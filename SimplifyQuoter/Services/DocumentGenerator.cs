using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Build the three import‐format DataTables.
    /// SheetA now enriches DESCRIPTION & ITEM GROUP via AI + cache (blocking on .Result).
    /// </summary>
    public class DocumentGenerator
    {
        public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var sheetA = BuildSheetA(
                infoRows ?? Enumerable.Empty<RowView>(),
                insideRows ?? Enumerable.Empty<RowView>());
            var sheetB = new DataTable("SheetB");
            var sheetC = new DataTable("SheetC");
            return new List<DataTable> { sheetA, sheetB, sheetC };
        }

        private DataTable BuildSheetA(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var dt = new DataTable("SheetA");
            dt.Columns.Add("Item Code");
            dt.Columns.Add("PART#");
            dt.Columns.Add("BRAND");
            dt.Columns.Add("Item Group");
            dt.Columns.Add("DESCRIPTION");
            dt.Columns.Add("Purchasing UOM");
            dt.Columns.Add("Sales UOM");
            dt.Columns.Add("Inventory UOM");
            dt.Columns.Add("Vendor Code");

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY",
                              StringComparison.OrdinalIgnoreCase);

            // INFO_EXCEL rows
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;
                var brand = c.Length > 2 ? c[2].Trim() : string.Empty;
                var vendor = c.Length > 6 ? c[6].Trim() : string.Empty;

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer
                                        .GetItemGroupAsync(code, brand, vendor)
                                        .Result;
                row["DESCRIPTION"] = Transformer
                                        .GetDescriptionAsync(code)
                                        .Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = vendor;

                dt.Rows.Add(row);
            }

            // INSIDE_EXCEL rows
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : "string.Empty";
                var brand = c.Length > 1 ? c[1].Trim() : "string.Empty";
                var vendor = c.Length > 6 ? c[6].Trim() : "string.Empty";

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer
                                        .GetItemGroupAsync(code, brand, vendor)
                                        .Result;
                row["DESCRIPTION"] = Transformer
                                        .GetDescriptionAsync(code)
                                        .Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = "VL000442";

                dt.Rows.Add(row);
            }

            return dt;
        }
    }
}
