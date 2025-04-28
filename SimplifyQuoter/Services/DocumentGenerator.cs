using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Build the three import‐format DataTables.
    /// SheetA now enriches DESCRIPTION via AI + cache (blocking on .Result).
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
                String.Equals(rv.Cells[14]?.Trim(), "READY",
                              StringComparison.OrdinalIgnoreCase);

            // INFO_EXCEL rows
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : String.Empty;
                var brand = c.Length > 2 ? c[2].Trim() : String.Empty;
                var r = dt.NewRow();
                r["Item Code"] = "H-" + code;
                r["PART#"] = code;
                r["BRAND"] = brand;
                r["Item Group"] = String.Empty;
                // *this* is the blocking AI call:
                r["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Code"] = "VL000442";
                dt.Rows.Add(r);
            }

            // INSIDE_EXCEL rows
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : String.Empty;
                var brand = c.Length > 1 ? c[1].Trim() : String.Empty;
                var r = dt.NewRow();
                r["Item Code"] = "H-" + code;
                r["PART#"] = code;
                r["BRAND"] = brand;
                r["Item Group"] = String.Empty;
                r["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Code"] = "VL000442";
                dt.Rows.Add(r);
            }

            return dt;
        }
    }
}
