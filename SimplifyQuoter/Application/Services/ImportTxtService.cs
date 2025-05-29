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
        /// Writes SheetA/B/C and updates import_row/process_job for Sheet A.
        /// Reports row-by-row logs (including cell values) and progress.
        /// </summary>
        public List<string> ProcessImport(
            Guid importFileId,
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows,
            IProgress<string> log,
            IProgress<double> percent)
        {
            // 1) collect only READY rows
            var readyRows = infoRows.Concat(insideRows)
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

            var outPaths = new List<string>();
            percent?.Report(0);

            // --- Sheet A ---
            log?.Report("Sheet A working…");
            percent?.Report(5);

            // **PASS BOTH** log + percent into the generator now:
            var sheets = _docGen.GenerateImportSheets(infoRows, insideRows, log, percent);
            var sheetA = sheets[0];
            int totalA = sheetA.Rows.Count;   // should equal readyRows.Count
            string pathA = Path.Combine(_tempDir, "SheetA.txt");

            using (var writerA = new StreamWriter(pathA, false, Encoding.UTF8))
            {
                for (int i = 0; i < totalA; i++)
                {
                    var dr = sheetA.Rows[i];
                    var vals = sheetA.Columns
                                      .Cast<DataColumn>()
                                      .Select(c => dr[c]?.ToString() ?? string.Empty)
                                      .ToArray();
                    writerA.WriteLine(string.Join("\t", vals));

                    // update import_row + process_job counts for READY rows
                    var rv = readyRows[i];
                    using (var db2 = new DatabaseService())
                    using (var tx = db2.Connection.BeginTransaction())
                    {
                        var c1 = db2.Connection.CreateCommand();
                       c1.Transaction = tx;
                        c1.CommandText = @"
UPDATE import_row
   SET processed   = TRUE,
       exec_count  = exec_count + 1
 WHERE id = @rid";
                        c1.Parameters.AddWithValue("rid", rv.RowId);
                        c1.ExecuteNonQuery();

                        var c2 = db2.Connection.CreateCommand();
                        c2.Transaction = tx;
                        c2.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                        c2.Parameters.AddWithValue("jid", jobId);
                        c2.ExecuteNonQuery();

                        tx.Commit();
                    }

                    // log + bar already handled inside BuildSheetA
                    // but we can still repeat if desired:
                    log?.Report($"  • Sheet A row {i + 1}/{totalA}: {string.Join(" | ", vals)}");
                    // percent also updated by the generator, so this is optional:
                    double pA = 5 + 65.0 * (i + 1) / totalA;
                    percent?.Report(pA);
                }
            }
            outPaths.Add(pathA);
            log?.Report("Sheet A complete");
            percent?.Report(70);

            // --- Sheet B ---
            log?.Report("Sheet B working…");
            percent?.Report(70);

            var sheetB = sheets[1];
            int totalB = sheetB.Rows.Count;
            string pathB = Path.Combine(_tempDir, "SheetB.txt");

            using (var writerB = new StreamWriter(pathB, false, Encoding.UTF8))
            {
                for (int j = 0; j < totalB; j++)
                {
                    var dr = sheetB.Rows[j];
                    var vals = sheetB.Columns
                                      .Cast<DataColumn>()
                                      .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writerB.WriteLine(string.Join("\t", vals));

                    // percent updated by BuildSheetB; repeat if needed:
                    double pB = 70 + 15.0 * (j + 1) / totalB;
                    percent?.Report(pB);
                }
            }
            outPaths.Add(pathB);
            log?.Report("Sheet B complete");
            percent?.Report(85);

            // --- Sheet C ---
            log?.Report("Sheet C working…");
            percent?.Report(85);

            var sheetC = sheets[2];
            int totalC = sheetC.Rows.Count;
            string pathC = Path.Combine(_tempDir, "SheetC.txt");

            using (var writerC = new StreamWriter(pathC, false, Encoding.UTF8))
            {
                for (int k = 0; k < totalC; k++)
                {
                    var dr = sheetC.Rows[k];
                    var vals = sheetC.Columns
                                      .Cast<DataColumn>()
                                      .Select(c => dr[c]?.ToString() ?? string.Empty);
                    writerC.WriteLine(string.Join("\t", vals));

                    double pC = 85 + 15.0 * (k + 1) / totalC;
                    percent?.Report(pC);
                }
            }
            outPaths.Add(pathC);
            log?.Report("Sheet C complete");
            percent?.Report(100);

            // --- finalize the job ---
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

            log?.Report("--- Import complete ---");
            return outPaths;
        }


        /// <summary>
        /// Deletes all rows in the part table and returns how many were removed.
        /// </summary>
        public int CleanupParts()
        {
            using (var db = new DatabaseService())
                return db.CleanupParts();
        }

        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            throw new NotImplementedException();
        }
    }
}
