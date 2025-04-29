// Services/DocumentGenerator.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Build the three import‐format DataTables:
    ///  - SheetA merges INFO_EXCEL + INSIDE_EXCEL with AI‐enriched DESCRIPTION & ITEM GROUP
    ///  - SheetB builds your price‐list data
    ///  - SheetC left as a placeholder
    /// </summary>
    public class DocumentGenerator
    {
        public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var sheetA = BuildSheetA(infoRows ?? Enumerable.Empty<RowView>(),
                                     insideRows ?? Enumerable.Empty<RowView>());
            var sheetB = BuildSheetB(infoRows ?? Enumerable.Empty<RowView>(),
                                     insideRows ?? Enumerable.Empty<RowView>());
            var sheetC = BuildSheetC();  // to be implemented later

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
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            // INFO_EXCEL rows
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;  // Column D
                var brand = c.Length > 2 ? c[2].Trim() : string.Empty;  // Column C
                var vendor = c.Length > 6 ? c[6].Trim() : string.Empty;  // Column G

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                row["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = "VL000416";

                dt.Rows.Add(row);
            }

            // INSIDE_EXCEL rows
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;  // Column C
                var brand = c.Length > 1 ? c[1].Trim() : string.Empty;  // Column B
                var vendor = c.Length > 6 ? c[6].Trim() : string.Empty;  // Column G
                var weight = c.Length > 11 ? c[11].Trim() : string.Empty; // Column L (if you want to include)

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                // you can append weight or other free-text info here if desired
                row["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = "VL000416";

                dt.Rows.Add(row);
            }

            return dt;
        }

        private DataTable BuildSheetB(
    IEnumerable<RowView> infoRows,
    IEnumerable<RowView> insideRows)
        {
            var dt = new DataTable("SheetB");
            dt.Columns.Add("Price List No");
            dt.Columns.Add("Item No.");
            dt.Columns.Add("Item No.Base Price List No.");
            dt.Columns.Add("Item No, Factor");
            dt.Columns.Add("Item No, List Price");
            dt.Columns.Add("Item No, Currency for List Price");
            dt.Columns.Add("Item No, Additional Price (1)");
            dt.Columns.Add("Item No, Currency for Add. Price 1");
            dt.Columns.Add("Item No, Additional Price (2)");
            dt.Columns.Add("Item No, Currency for Add. Price 2");
            dt.Columns.Add("Item No, With UOM");
            dt.Columns.Add("Item No, With UOM, UOM Entry");
            dt.Columns.Add("Item No, With UOM, UOM Price");
            dt.Columns.Add("Item No, With UOM, Reduced By %");
            dt.Columns.Add("Item No, With UOM, Additional Price (1)");
            dt.Columns.Add("Item No, With UOM, Reduced By 2");
            dt.Columns.Add("Item No, With UOM, Additional Price (2)");
            dt.Columns.Add("Item No. With UOM, Reduced By %");

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            const string priceListNo = "11";
            const string factor = "1";
            const string uom = "EACH";

            // INFO_EXCEL: Item No. = Column D (idx 3), List Price = Column K (idx 10)
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;
                var listPrice = c.Length > 10 ? c[10].Trim() : string.Empty;

                var row = dt.NewRow();
                row["Price List No"] = priceListNo;
                row["Item No."] = code;
                row["Item No.Base Price List No."] = priceListNo;
                row["Item No, Factor"] = factor;
                row["Item No, List Price"] = listPrice;
                row["Item No, Currency for List Price"] = string.Empty;
                row["Item No, Additional Price (1)"] = string.Empty;
                row["Item No, Currency for Add. Price 1"] = string.Empty;
                row["Item No, Additional Price (2)"] = string.Empty;
                row["Item No, Currency for Add. Price 2"] = string.Empty;
                row["Item No, With UOM"] = uom;
                row["Item No, With UOM, UOM Entry"] = uom;
                row["Item No, With UOM, UOM Price"] = uom;
                row["Item No, With UOM, Reduced By %"] = string.Empty;
                row["Item No, With UOM, Additional Price (1)"] = string.Empty;
                row["Item No, With UOM, Reduced By 2"] = string.Empty;
                row["Item No, With UOM, Additional Price (2)"] = string.Empty;
                row["Item No. With UOM, Reduced By %"] = string.Empty;

                dt.Rows.Add(row);
            }

            // INSIDE_EXCEL: Item No. = Column C (idx 2), List Price = Column J (idx 9)
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;
                var listPrice = c.Length > 9 ? c[9].Trim() : string.Empty;

                var row = dt.NewRow();
                row["Price List No"] = priceListNo;
                row["Item No."] = code;
                row["Item No.Base Price List No."] = priceListNo;
                row["Item No, Factor"] = factor;
                row["Item No, List Price"] = listPrice;
                row["Item No, Currency for List Price"] = string.Empty;
                row["Item No, Additional Price (1)"] = string.Empty;
                row["Item No, Currency for Add. Price 1"] = string.Empty;
                row["Item No, Additional Price (2)"] = string.Empty;
                row["Item No, Currency for Add. Price 2"] = string.Empty;
                row["Item No, With UOM"] = uom;
                row["Item No, With UOM, UOM Entry"] = uom;
                row["Item No, With UOM, UOM Price"] = uom;
                row["Item No, With UOM, Reduced By %"] = string.Empty;
                row["Item No, With UOM, Additional Price (1)"] = string.Empty;
                row["Item No, With UOM, Reduced By 2"] = string.Empty;
                row["Item No, With UOM, Additional Price (2)"] = string.Empty;
                row["Item No. With UOM, Reduced By %"] = string.Empty;

                dt.Rows.Add(row);
            }

            return dt;
        }


        private DataTable BuildSheetC()
        {
            // not yet implemented
            return new DataTable("SheetC");
        }
    }
}
