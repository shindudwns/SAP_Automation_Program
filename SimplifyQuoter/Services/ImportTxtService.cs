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
            _tempDir = Path.Combine(
                Path.GetTempPath(), "SimplifyQuoter_Import");
            if (!Directory.Exists(_tempDir))
                Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Synchronously writes SheetA/B/C and updates import_row/process_job for A only.
        /// Reports progress via the IProgress<string>.
        /// </summary>
        public List<string> ProcessImport(
    Guid importFileId,
    IEnumerable<RowView> infoRows,
    IEnumerable<RowView> insideRows,
    IProgress<string> progress,
    IProgress<double> percent)
        {
            // 0% → just started
            progress?.Report("Starting import…");
            percent?.Report(0);

            // 1) collect READY rows
            var readyRows = infoRows
                .Concat(insideRows)
                .Where(rv =>
                    rv.Cells.Length > 14 &&
                    string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase))
                .ToList();
            progress?.Report($"Found {readyRows.Count} READY rows");
            percent?.Report(5);

            // insert process_job
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

            // --- Sheet A ---
            progress?.Report("Sheet A working…");
            var sheets = _docGen.GenerateImportSheets(infoRows, insideRows);
            var sheetA = sheets[0];
            string pathA = Path.Combine(_tempDir, "SheetA.txt");
            using (var writer = new StreamWriter(pathA, false, Encoding.UTF8))
            {
                for (int i = 0; i < sheetA.Rows.Count; i++)
                {
                    var dr = sheetA.Rows[i];
                    var vals = sheetA.Columns
                                     .Cast<DataColumn>()
                                     .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writer.WriteLine(string.Join("\t", vals));

                    if (i < readyRows.Count)
                    {
                        // update import_row & process_job
                        var rv = readyRows[i];
                        using (var db2 = new DatabaseService())
                        using (var tx = db2.Connection.BeginTransaction())
                        {
                            using (var c1 = db2.Connection.CreateCommand())
                            {
                                c1.Transaction = tx;
                                c1.CommandText = @"
UPDATE import_row
   SET processed   = TRUE,
       exec_count  = exec_count + 1
 WHERE id = @rid";
                                c1.Parameters.AddWithValue("rid", rv.RowId);
                                c1.ExecuteNonQuery();
                            }
                            using (var c2 = db2.Connection.CreateCommand())
                            {
                                c2.Transaction = tx;
                                c2.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                                c2.Parameters.AddWithValue("jid", jobId);
                                c2.ExecuteNonQuery();
                            }
                            tx.Commit();
                        }

                        // per‐row log + percent
                        progress?.Report($"  ● Row {i + 1}/{readyRows.Count} done");
                        // map rows → 5%–35% of the bar
                        double p = 5 + 30.0 * (i + 1) / readyRows.Count;
                        percent?.Report(p);
                    }
                }
            }
            progress?.Report("Sheet A complete");
            percent?.Report(35);

            // --- Sheet B ---
            progress?.Report("Sheet B working…");
            var sheetB = sheets[1];
            string pathB = Path.Combine(_tempDir, "SheetB.txt");
            using (var writer = new StreamWriter(pathB, false, Encoding.UTF8))
            {
                foreach (DataRow dr in sheetB.Rows)
                {
                    var vals = sheetB.Columns
                                     .Cast<DataColumn>()
                                     .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writer.WriteLine(string.Join("\t", vals));
                }
            }
            progress?.Report("Sheet B complete");
            percent?.Report(70);

            // --- Sheet C ---
            progress?.Report("Sheet C working…");
            var sheetC = sheets[2];
            string pathC = Path.Combine(_tempDir, "SheetC.txt");
            using (var writer = new StreamWriter(pathC, false, Encoding.UTF8))
            {
                foreach (DataRow dr in sheetC.Rows)
                {
                    var vals = sheetC.Columns
                                     .Cast<DataColumn>()
                                     .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writer.WriteLine(string.Join("\t", vals));
                }
            }
            progress?.Report("Sheet C complete");

            // finalize
            using (var db3 = new DatabaseService())
            using (var cmd3 = db3.Connection.CreateCommand())
            {
                cmd3.CommandText = @"
UPDATE process_job
   SET completed_at = NOW()
 WHERE id = @jid";
                cmd3.Parameters.AddWithValue("jid", jobId);
                cmd3.ExecuteNonQuery();
            }

            progress?.Report("--- Import complete ---");
            percent?.Report(100);

            return new List<string> { pathA, pathB, pathC };
        }




        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException();
        }
    }
}
