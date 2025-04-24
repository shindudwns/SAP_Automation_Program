using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using OfficeOpenXml;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    public class ExcelService
    {
        static ExcelService()
        {
            // NEW for EPPlus 8+: choose one of these:
            // Non-commercial personal use:
            ExcelPackage.License.SetNonCommercialPersonal("Your Name");
            // – or – Non-commercial organization:
            // ExcelPackage.License.SetNonCommercialOrganization("My Org Name");
            //
            // (Don’t call the obsolete LicenseContext property any more.)
        }

        /// <summary>
        /// For Import-TXT flow: persists into import_file & import_row tables.
        /// </summary>
        public Tuple<Guid, ObservableCollection<RowView>> LoadImportSheetViaDialog(string fileType)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select Import Excel"
            };
            if (dlg.ShowDialog() != true)
                return Tuple.Create(Guid.Empty, (ObservableCollection<RowView>)null);

            var rows = ReadWorksheetIntoRows(dlg.FileName);

            Guid fileId;
            using (var db = new DatabaseService())
            using (var tx = db.Connection.BeginTransaction())
            {
                // insert import_file
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO import_file(filename,file_type)
VALUES(@fn,@ft)
RETURNING id";
                    cmd.Parameters.AddWithValue("fn", Path.GetFileName(dlg.FileName));
                    cmd.Parameters.AddWithValue("ft", fileType);
                    fileId = (Guid)cmd.ExecuteScalar();
                }

                // insert import_row
                foreach (var rv in rows)
                {
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
INSERT INTO import_row(
  file_id,row_index,cells,is_ready)
VALUES(
  @fid,@idx,@cells,@ready)
RETURNING id";
                        cmd.Parameters.AddWithValue("fid", fileId);
                        cmd.Parameters.AddWithValue("idx", rv.RowIndex);
                        cmd.Parameters.AddWithValue("cells", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonConvert.SerializeObject(rv.Cells));

                        //cmd.Parameters.AddWithValue("cells", JsonConvert.SerializeObject(rv.Cells));
                        bool ready = rv.Cells.Length > 14 &&
                                     string.Equals(rv.Cells[14]?.Trim(),
                                                   "READY",
                                                   StringComparison.OrdinalIgnoreCase);
                        cmd.Parameters.AddWithValue("ready", ready);

                        rv.RowId = (Guid)cmd.ExecuteScalar();
                    }
                }

                tx.Commit();
            }

            return Tuple.Create(fileId, rows);
        }

        /// <summary>
        /// For SAP-automation flow: persists into sap_file & sap_row tables.
        /// </summary>
        public Tuple<Guid, ObservableCollection<RowView>> LoadSapSheetViaDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select SMK_EXCEL for SAP Automation"
            };
            if (dlg.ShowDialog() != true)
                return Tuple.Create(Guid.Empty, (ObservableCollection<RowView>)null);

            var rows = ReadWorksheetIntoRows(dlg.FileName);

            Guid fileId;
            using (var db = new DatabaseService())
            using (var tx = db.Connection.BeginTransaction())
            {
                // insert sap_file
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO sap_file(filename)
VALUES(@fn)
RETURNING id";
                    cmd.Parameters.AddWithValue("fn", Path.GetFileName(dlg.FileName));
                    fileId = (Guid)cmd.ExecuteScalar();
                }

                // insert sap_row
                foreach (var rv in rows)
                {
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
INSERT INTO sap_row(
  file_id,row_index,cells,is_ready,part_code)
VALUES(
  @fid,@idx,@cells,@ready,@pc)
RETURNING id";
                        cmd.Parameters.AddWithValue("fid", fileId);
                        cmd.Parameters.AddWithValue("idx", rv.RowIndex);
                        //cmd.Parameters.AddWithValue("cells", JsonConvert.SerializeObject(rv.Cells));
                        cmd.Parameters.AddWithValue(
                            "cells",
                            NpgsqlDbType.Jsonb,
                            JsonConvert.SerializeObject(rv.Cells)
                        );

                        bool ready = rv.Cells.Length > 14 &&
                                     string.Equals(rv.Cells[14]?.Trim(),
                                                   "READY",
                                                   StringComparison.OrdinalIgnoreCase);
                        cmd.Parameters.AddWithValue("ready", ready);
                        // default to column C as part_code:
                        cmd.Parameters.AddWithValue("pc",
                            rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty);

                        rv.RowId = (Guid)cmd.ExecuteScalar();
                    }
                }

                tx.Commit();
            }

            return Tuple.Create(fileId, rows);
        }

        /// <summary>
        /// Legacy alias: returns the rows only (no file ID).
        /// Internally uses LoadSapSheetViaDialog().
        /// </summary>
        public ObservableCollection<RowView> LoadSheetViaDialog()
        {
            var tmp = LoadSapSheetViaDialog();
            return tmp == null ? null : tmp.Item2;
        }

        /// <summary>
        /// Helper to read the first worksheet into RowView[].
        /// </summary>
        private ObservableCollection<RowView> ReadWorksheetIntoRows(string path)
        {
            var rows = new ObservableCollection<RowView>();
            using (var pkg = new ExcelPackage(new FileInfo(path)))
            {
                var ws = pkg.Workbook.Worksheets[0];
                int cols = ws.Dimension.End.Column;
                int lastRow = ws.Dimension.End.Row;

                for (int r = 2; r <= lastRow; r++)
                {
                    var rv = new RowView
                    {
                        RowIndex = r,
                        Cells = new string[cols]
                    };
                    for (int c = 1; c <= cols; c++)
                        rv.Cells[c - 1] = ws.Cells[r, c].Text;
                    rows.Add(rv);
                }
            }
            return rows;
        }
    }
}
