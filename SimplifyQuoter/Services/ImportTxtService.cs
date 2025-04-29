// Services/ImportTxtService.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Npgsql;
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
            if (!Directory.Exists(_tempDir))
                Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Synchronously writes SheetA.txt, SheetB.txt, SheetC.txt
        /// and updates import_row/process_job for SheetA only.
        /// </summary>
        public List<string> ProcessImport(
            Guid importFileId,
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            // 1) collect only READY rows
            var readyRows = infoRows
                .Concat(insideRows)
                .Where(rv =>
                    rv.Cells.Length > 14 &&
                    string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 2) insert process_job
            Guid jobId;
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(import_file_id,job_type,total_rows)
VALUES(@fid,'IMPORT_TXT',@tot)
RETURNING id";
                cmd.Parameters.AddWithValue("fid", importFileId);
                cmd.Parameters.AddWithValue("tot", readyRows.Count);
                jobId = (Guid)cmd.ExecuteScalar();
            }

            // 3) build all three sheets
            IList<DataTable> sheets = _docGen.GenerateImportSheets(infoRows, insideRows);

            // 4) for each sheet, dump to TXT and track path
            var outPaths = new List<string>();
            foreach (var dt in sheets)
            {
                string path = Path.Combine(_tempDir, dt.TableName + ".txt");
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        var vals = dt.Columns
                                     .Cast<DataColumn>()
                                     .Select(c => dr[c]?.ToString() ?? string.Empty);
                        writer.WriteLine(string.Join("\t", vals));
                    }
                }
                outPaths.Add(path);

                // only SheetA needs import_row/process_job updates:
                if (dt.TableName == "SheetA")
                {
                    for (int i = 0; i < readyRows.Count; i++)
                    {
                        var rv = readyRows[i];
                        using (var db = new DatabaseService())
                        using (var tx = db.Connection.BeginTransaction())
                        {
                            using (var cmd = db.Connection.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = @"
UPDATE import_row
   SET processed  = TRUE,
       exec_count = exec_count + 1
 WHERE id = @rid";
                                cmd.Parameters.AddWithValue("rid", rv.RowId);
                                cmd.ExecuteNonQuery();
                            }
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

            return outPaths;
        }

        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException();
        }
    }
}
