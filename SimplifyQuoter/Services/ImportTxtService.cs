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
        /// Writes SheetA.txt and tracks import_row/process_job.
        /// </summary>
        public string ProcessImport(
            Guid importFileId,
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            // 1) pick out only the READY RowViews
            var readyRows = infoRows
                              .Concat(insideRows)
                              .Where(rv =>
                                  rv.Cells.Length > 14 &&
                                  string.Equals(rv.Cells[14]?.Trim(),
                                                "READY",
                                                StringComparison.OrdinalIgnoreCase))
                              .ToList();
            int total = readyRows.Count;

            // 2) create a process_job record
            Guid jobId;
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(import_file_id,job_type,total_rows)
VALUES(@fid,'IMPORT_TXT',@tot) RETURNING id";
                cmd.Parameters.AddWithValue("fid", importFileId);
                cmd.Parameters.AddWithValue("tot", total);
                jobId = (Guid)cmd.ExecuteScalar();
            }

            // 3) build the DataTable mappings (only the 9 desired columns)
            var sheets = _docGen.GenerateImportSheets(infoRows, insideRows);
            var sheetA = sheets[0];    // DataTable with columns: Item Code, PART#, BRAND, …

            // 4) write out the TXT by iterating the DataTable rows
            //    and in parallel update each corresponding RowView in readyRows
            string outPath = Path.Combine(_tempDir, sheetA.TableName + ".txt");
            using (var writer = new StreamWriter(outPath, false, Encoding.UTF8))
            {
                for (int i = 0; i < readyRows.Count; i++)
                {
                    var dr = sheetA.Rows[i];
                    // extract each column’s value from the DataRow
                    var vals = sheetA.Columns
                                    .Cast<DataColumn>()
                                    .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writer.WriteLine(string.Join("\t", vals));

                    // now update import_row.processed & process_job.processed_rows
                    var rv = readyRows[i];
                    using (var db = new DatabaseService())
                    using (var tx = db.Connection.BeginTransaction())
                    {
                        // mark that row processed
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
                        // bump the job’s processed_rows
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

        /// <summary>
        /// Stub for SAP import via DI-API—implement later.
        /// </summary>
        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException("ImportIntoSap() to be implemented");
        }
    }
}
