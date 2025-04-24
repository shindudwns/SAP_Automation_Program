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

            // INFO_EXCEL: use D for Item Code/PART#, C for BRAND
            foreach (var rv in infoRows.Where(rv =>
                     rv.Cells.Length > 14 &&
                     rv.Cells[14].Trim().Equals("READY", StringComparison.OrdinalIgnoreCase)))
            {
                var c = rv.Cells;
                var r = dt.NewRow();

                // Column D is index 3
                var code = c.Length > 3 ? c[3] : string.Empty;
                r["Item Code"] = code;
                r["PART#"] = code;

                // Column C is index 2
                r["BRAND"] = c.Length > 2 ? c[2] : string.Empty;

                r["Item Group"] = string.Empty;
                r["DESCRIPTION"] = string.Empty;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Name"] = string.Empty;   // or your default

                dt.Rows.Add(r);
            }

            // INSIDE_EXCEL: use C for Item Code/PART#, B for BRAND
            foreach (var rv in insideRows.Where(rv =>
                     rv.Cells.Length > 14 &&
                     rv.Cells[14].Trim().Equals("READY", StringComparison.OrdinalIgnoreCase)))
            {
                var c = rv.Cells;
                var r = dt.NewRow();

                // Column C is index 2
                var code = c.Length > 2 ? c[2] : string.Empty;
                r["Item Code"] = code;
                r["PART#"] = code;

                // Column B is index 1
                r["BRAND"] = c.Length > 1 ? c[1] : string.Empty;

                r["Item Group"] = string.Empty;
                r["DESCRIPTION"] = string.Empty;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Name"] = string.Empty;

                dt.Rows.Add(r);
            }

            return dt;
        }

    }
}
