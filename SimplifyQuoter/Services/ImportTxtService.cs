using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    public class ImportTxtService
    {
        private readonly DocumentGenerator _docGen;
        private readonly string _tempDir;

        public ImportTxtService(DocumentGenerator docGen)
        {
            _docGen = docGen;
            _tempDir = Path.Combine(Path.GetTempPath(), "SimplifyQuoter_Import");
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Writes SheetA.txt (only the mapped columns) and updates import_row/process_job.
        /// </summary>
        public string ProcessImport(
            Guid importFileId,
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            // 1) filter only READY rows
            var readyRows = infoRows
                              .Concat(insideRows)
                              .Where(rv =>
                                  rv.Cells.Length > 14 &&
                                  string.Equals(rv.Cells[14]?.Trim(),
                                                "READY",
                                                StringComparison.OrdinalIgnoreCase))
                              .ToList();

            // 2) create a job
            Guid jobId;
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(import_file_id,job_type,total_rows)
VALUES(@fid,'IMPORT_TXT',@tot) RETURNING id";
                cmd.Parameters.AddWithValue("fid", importFileId);
                cmd.Parameters.AddWithValue("tot", readyRows.Count);
                jobId = (Guid)cmd.ExecuteScalar();
            }

            // 3) build the mapped DataTable
            var sheetA = _docGen.GenerateImportSheets(infoRows, insideRows)[0];

            // 4) export by reading the DataTable rows
            var outPath = Path.Combine(_tempDir, sheetA.TableName + ".txt");
            using (var writer = new StreamWriter(outPath, false, Encoding.UTF8))
            {
                for (int i = 0; i < readyRows.Count; i++)
                {
                    DataRow dr = sheetA.Rows[i];
                    // read exactly the mapped columns
                    var vals = sheetA.Columns
                                     .Cast<DataColumn>()
                                     .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writer.WriteLine(string.Join("\t", vals));

                    // update import_row and process_job for this RowView
                    var rv = readyRows[i];
                    using (var db = new DatabaseService())
                    using (var tx = db.Connection.BeginTransaction())
                    {
                        // mark the import_row
                        using (var cmd = db.Connection.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
UPDATE import_row
   SET processed   = TRUE,
       exec_count  = exec_count + 1
 WHERE id = @rid";
                            cmd.Parameters.AddWithValue("rid", rv.RowId);
                            cmd.ExecuteNonQuery();
                        }
                        // bump the job counter
                        using (var cmd = db.Connection.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                            cmd.Parameters.AddWithValue("jid", jobId);
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                    }
                }
            }

            // 5) finalize the job
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE process_job
   SET completed_at = NOW()
 WHERE id = @jid";
                cmd.Parameters.AddWithValue("jid", jobId);
                cmd.ExecuteNonQuery();
            }

            return outPath;
        }

        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException();
        }
    }
}
