// Services/ImportTxtService.cs

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
            var ready = infoRows.Concat(insideRows)
                .Where(rv => rv.Cells.Length > 14
                          && rv.Cells[14].Trim().Equals("READY", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 2) start job
            Guid jobId;
            using (var db = new DatabaseService())
            using (var c = db.Connection.CreateCommand())
            {
                c.CommandText = @"
INSERT INTO process_job(import_file_id,job_type,total_rows)
VALUES(@fid,'IMPORT_TXT',@tot)
RETURNING id";
                c.Parameters.AddWithValue("fid", importFileId);
                c.Parameters.AddWithValue("tot", ready.Count);
                jobId = (Guid)c.ExecuteScalar();
            }

            // 3) build sheets
            var sheets = _docGen.GenerateImportSheets(infoRows, insideRows);

            // 4) dump each to TXT
            var paths = new List<string>();
            foreach (var dt in sheets)
            {
                var path = Path.Combine(_tempDir, dt.TableName + ".txt");
                using (var w = new StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        var line = string.Join("\t",
                            dt.Columns.Cast<DataColumn>()
                              .Select(col => dr[col]?.ToString() ?? ""));
                        w.WriteLine(line);
                    }
                }
                paths.Add(path);

                // update only SheetA rows in DB
                if (dt.TableName == "SheetA")
                {
                    for (int i = 0; i < ready.Count; i++)
                    {
                        var rv = ready[i];
                        using (var db = new DatabaseService())
                        using (var tx = db.Connection.BeginTransaction())
                        {
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

            // 5) finalize
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

            return paths;
        }

        public void ImportIntoSap(IEnumerable<string> txtFiles)
            => throw new NotImplementedException();
    }
}
