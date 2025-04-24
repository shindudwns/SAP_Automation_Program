using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    public class DocumentGenerator
    {
        /// <summary>
        /// Build the three import‐format DataTables.
        /// SheetA now merges two sources with distinct column mappings.
        /// </summary>
        public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var sheetA = BuildSheetA(infoRows ?? Enumerable.Empty<RowView>(),
                                    insideRows ?? Enumerable.Empty<RowView>());

            // placeholders for B & C (to be implemented later)
            var sheetB = new DataTable("SheetB");
            var sheetC = new DataTable("SheetC");

            return new List<DataTable> { sheetA, sheetB, sheetC };
        }

        private DataTable BuildSheetA(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            // 1) Define the columns (headers are *not* written to the .txt)
            var dt = new DataTable("SheetA");
            dt.Columns.Add("Item Code");
            dt.Columns.Add("PART#");
            dt.Columns.Add("BRAND");
            dt.Columns.Add("Item Group");
            dt.Columns.Add("DESCRIPTION");
            dt.Columns.Add("Purchasing UOM");
            dt.Columns.Add("Sales UOM");
            dt.Columns.Add("Inventory UOM");
            dt.Columns.Add("Vendor Name");

            // 2) Filter & map INFO_EXCEL rows (Column O == "READY")
            foreach (var rv in infoRows.Where(rv =>
                     rv.Cells.Length > 14
                     && string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase)))
            {
                var c = rv.Cells;
                var r = dt.NewRow();
                // INFO_EXCEL uses Column D (index 3) for Item Code & PART#
                r["Item Code"] = c.Length > 3 ? "H-" + c[3] : "string.Empty";
                r["PART#"] = c.Length > 3 ? c[3] : "string.Empty";
                // BRAND comes from Column C (index 2)  
                r["BRAND"] = c.Length > 2 ? c[2] : "string.Empty";

                r["Item Group"] = "string.Empty";
                r["DESCRIPTION"] = "string.Empty";
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Name"] = "VL000442";

                dt.Rows.Add(r);
            }

            // 3) Filter & map INSIDE_EXCEL rows (Column O == "READY")
            foreach (var rv in insideRows.Where(rv =>
                     rv.Cells.Length > 14
                     && string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase)))
            {
                var c = rv.Cells;
                var r = dt.NewRow();
                // INSIDE_EXCEL uses Column C (index 2) for Item Code & PART#
                r["Item Code"] = c.Length > 2 ? "H-" + c[2] : "string.Empty";
                r["PART#"] = c.Length > 2 ? c[2] : "string.Empty";
                // BRAND comes from Column B (index 1)
                r["BRAND"] = c.Length > 1 ? c[1] : "string.Empty";

                r["Item Group"] = "string.Empty";
                r["DESCRIPTION"] = "string.Empty";
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Name"] = "VL000442";

                dt.Rows.Add(r);
            }

            return dt;
        }
    }
}
