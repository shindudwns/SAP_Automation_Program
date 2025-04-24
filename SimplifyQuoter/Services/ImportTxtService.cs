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
            var allRows = infoRows.Concat(insideRows);
            var readyRows = allRows.Where(rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(),
                              "READY",
                              StringComparison.OrdinalIgnoreCase))
                                   .ToList();
            int total = readyRows.Count;

            // create job
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

            // generate sheets
            IList<DataTable> sheets = _docGen.GenerateImportSheets(infoRows, insideRows);
            DataTable sheetA = sheets[0];
            string outPath = Path.Combine(_tempDir, sheetA.TableName + ".txt");

            // write file + update DB
            using (var writer = new StreamWriter(outPath, false, Encoding.UTF8))
            {
                foreach (var rv in readyRows)
                {
                    // write columns from sheetA
                    var vals = sheetA.Columns.Cast<DataColumn>()
                        .Select((col, i) => rv.Cells.Length > i ? rv.Cells[i] : string.Empty)
                        .ToArray();
                    writer.WriteLine(string.Join("\t", vals));

                    // update import_row & process_job
                    using (var db = new DatabaseService())
                    using (var tx = db.Connection.BeginTransaction())
                    {
                        using (var cmd = db.Connection.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
UPDATE import_row
   SET processed = TRUE,
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

            // finalize job
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
        /// Stubbed SAP data import; you'll hook this up to your DI‐API later.
        /// </summary>
        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException("ImportIntoSap() to be implemented");
        }
    }
}
