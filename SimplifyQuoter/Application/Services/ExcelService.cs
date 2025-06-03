// File: Services/ExcelService.cs
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
using OfficeOpenXml.Style; 
using SimplifyQuoter.Services.ServiceLayer.Dtos;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SimplifyQuoter.Services
{
    public class ExcelService
    {
        public static readonly ExcelService Instance = new ExcelService();

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
                        cmd.Parameters.AddWithValue("cells", NpgsqlDbType.Jsonb, JsonConvert.SerializeObject(rv.Cells));

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
        /// New method: load SAP Excel from a given file path (no database inserts).
        /// Returns a new GUID (to stand in for sap_file.id) and the list of RowView.
        /// </summary>
        public Tuple<Guid, ObservableCollection<RowView>> LoadSapSheet(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Excel file not found.", path);

            var rows = ReadWorksheetIntoRows(path);
            var fileId = Guid.NewGuid(); // placeholder, since no DB persistence
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

                // ALWAYS read through column O (15), even if Excel's used‐range is smaller:
                int minCols = 15;
                int usedCols = ws.Dimension?.End.Column ?? 0;
                int cols = Math.Max(usedCols, minCols);

                int lastRow = ws.Dimension?.End.Row ?? 0;
                for (int r = 2; r <= lastRow; r++)
                {
                    var rv = new RowView
                    {
                        RowIndex = r,
                        Cells = new string[cols]
                    };
                    for (int c = 1; c <= cols; c++)
                    {
                        // ws.Cells[r,c].Text will be "" if truly blank
                        rv.Cells[c - 1] = ws.Cells[r, c].Text;
                    }
                    rows.Add(rv);
                }
            }
            return rows;
        }

        /// <summary>
        /// Exports a list of ItemDto into a new Excel workbook at 'outputPath'.
        /// </summary>
        public async Task WriteItemMasterPreviewAsync(
            List<ItemDto> dtos,
            string outputPath)
        {
            // Ensure the directory exists:
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Delete existing file if present:
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            // Create a new ExcelPackage
            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var ws = package.Workbook.Worksheets.Add("ItemMasterPreview");

                // 1) Write the header row
                var headers = new[]
                {
                    "#", "Item No.", "Description", "Part Number",
                    "Item Group", "Preferred Vendor", "Purchasing UoM",
                    "Sales UoM", "Inventory UOM", "Purchasing Price",
                    "Sales Price"
                };

                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cells[1, c + 1].Value = headers[c];
                    ws.Cells[1, c + 1].Style.Font.Bold = true;
                }

                // 2) Write each ItemDto row
                for (int i = 0; i < dtos.Count; i++)
                {
                    var dto = dtos[i];
                    int row = i + 2; // data starts at row=2

                    ws.Cells[row, 1].Value = i + 1;                // sequence
                    ws.Cells[row, 2].Value = dto.ItemCode;         // Item No.
                    ws.Cells[row, 3].Value = dto.ItemName;         // Description
                    ws.Cells[row, 4].Value = dto.FrgnName;         // Part Number
                    ws.Cells[row, 5].Value = dto.ItmsGrpCod;       // Item Group
                    ws.Cells[row, 6].Value = dto.BPCode;           // Preferred Vendor
                    ws.Cells[row, 7].Value = dto.PurchaseUnit;     // Purchasing UoM
                    ws.Cells[row, 8].Value = dto.SalesUnit;        // Sales UoM
                    ws.Cells[row, 9].Value = dto.InventoryUOM;     // Inventory UOM
                    ws.Cells[row, 10].Value = dto.U_PurchasingPrice; // Purchasing Price
                    ws.Cells[row, 11].Value = dto.U_SalesPrice;      // Sales Price
                }

                // 3) Auto-fit columns
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // 4) Save the file
                await package.SaveAsync();
            }
        }

        /// <summary>
        /// Exports a list of QuotationDto into a new Excel workbook at 'outputPath'.
        /// </summary>
        public async Task WriteQuotationPreviewAsync(
            List<QuotationDto> dtos,
            string outputPath)
        {
            // Ensure the directory exists:
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var ws = package.Workbook.Worksheets.Add("QuotationPreview");

                // 1) Write the header row
                var headers = new[]
                {
                    "#", "Card Code", "Doc Date",
                    "Item Code", "Quantity", "Free Text"
                };
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cells[1, c + 1].Value = headers[c];
                    ws.Cells[1, c + 1].Style.Font.Bold = true;
                }

                // 2) Write each QuotationDto row (only use the first DocumentLine)
                for (int i = 0; i < dtos.Count; i++)
                {
                    var qdto = dtos[i];
                    var line = qdto.DocumentLines.Count > 0
                        ? qdto.DocumentLines[0]
                        : new QuotationLineDto();

                    int row = i + 2;
                    ws.Cells[row, 1].Value = i + 1;                      // sequence
                    ws.Cells[row, 2].Value = qdto.CardCode;              // Card Code
                    ws.Cells[row, 3].Value = qdto.DocDate.ToString("yyyy-MM-dd"); // Doc Date
                    ws.Cells[row, 4].Value = line.ItemCode;              // Item Code
                    ws.Cells[row, 5].Value = line.Quantity;              // Quantity
                    ws.Cells[row, 6].Value = line.FreeText;              // Free Text
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                await package.SaveAsync();
            }
        }

        /// <summary>
        /// Reads back a “review preview” Excel (either ItemMaster or Quotation format).
        /// Returns the rows as RowView[] so that you can call Transformer/Model logic again.
        /// </summary>
        public List<RowView> ReadReviewSheet(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}", path);

            var rows = new List<RowView>();
            using (var pkg = new ExcelPackage(new FileInfo(path)))
            {
                // Decide which sheet to read: assume first worksheet
                var ws = pkg.Workbook.Worksheets[0];

                // Detect how many columns are present:
                int lastCol = ws.Dimension?.End.Column ?? 0;
                int lastRow = ws.Dimension?.End.Row ?? 0;

                // We expect first row to be headers; data starts at row=2
                for (int r = 2; r <= lastRow; r++)
                {
                    var rv = new RowView
                    {
                        RowIndex = r,
                        Cells = new string[lastCol]
                    };

                    for (int c = 1; c <= lastCol; c++)
                    {
                        rv.Cells[c - 1] = ws.Cells[r, c].Text;
                    }

                    rows.Add(rv);
                }
            }
            return rows;
        }

    }
}
