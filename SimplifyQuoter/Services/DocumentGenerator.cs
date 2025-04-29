// Services/DocumentGenerator.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Build the three import‐format DataTables:
    ///  - SheetA merges INFO_EXCEL + INSIDE_EXCEL with AI‐enriched DESCRIPTION & ITEM GROUP
    ///  - SheetB builds your price‐list data (PL=11)
    ///  - SheetC builds your alternate price‐list (PL=12, ÷0.8)
    /// </summary>
    public class DocumentGenerator
    {
        public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var sheetA = BuildSheetA(infoRows, insideRows);
            var sheetB = BuildSheetB(infoRows, insideRows);
            var sheetC = BuildSheetC(infoRows, insideRows);
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
            const string vendor = "VL000442";

            // INFO_EXCEL rows
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;  // D
                var brand = c.Length > 2 ? c[2].Trim() : string.Empty; // C
                //var vendor = c.Length > 6 ? c[6].Trim() : string.Empty; // G

                var r = dt.NewRow();
                r["Item Code"] = "H-" + code;
                r["PART#"] = code;
                r["BRAND"] = brand;
                r["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                r["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Code"] = vendor;
                dt.Rows.Add(r);
            }

            // INSIDE_EXCEL rows
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;  // C
                var brand = c.Length > 1 ? c[1].Trim() : string.Empty; // B
                //var vendor = c.Length > 6 ? c[6].Trim() : string.Empty; // G

                var r = dt.NewRow();
                r["Item Code"] = "H-" + code;
                r["PART#"] = code;
                r["BRAND"] = brand;
                r["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                r["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                r["Purchasing UOM"] = "EACH";
                r["Sales UOM"] = "EACH";
                r["Inventory UOM"] = "EACH";
                r["Vendor Code"] = vendor;
                dt.Rows.Add(r);
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

            const string PL = "11";
            const string FX = "1";
            const string UOM = "EACH";

            // INFO_EXCEL: D=code, K=list-price
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;
                var listPrice = c.Length > 10 ? c[10].Trim() : string.Empty;

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = code;
                r["Item No.Base Price List No."] = PL;
                r["Item No, Factor"] = FX;
                r["Item No, List Price"] = listPrice;
                r["Item No, Currency for List Price"] = string.Empty;
                r["Item No, Additional Price (1)"] = string.Empty;
                r["Item No, Currency for Add. Price 1"] = string.Empty;
                r["Item No, Additional Price (2)"] = string.Empty;
                r["Item No, Currency for Add. Price 2"] = string.Empty;
                r["Item No, With UOM"] = UOM;
                r["Item No, With UOM, UOM Entry"] = UOM;
                r["Item No, With UOM, UOM Price"] = UOM;
                r["Item No, With UOM, Reduced By %"] = string.Empty;
                r["Item No, With UOM, Additional Price (1)"] = string.Empty;
                r["Item No, With UOM, Reduced By 2"] = string.Empty;
                r["Item No, With UOM, Additional Price (2)"] = string.Empty;
                r["Item No. With UOM, Reduced By %"] = string.Empty;
                dt.Rows.Add(r);
            }

            // INSIDE_EXCEL: C=code, J=list-price
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;
                var listPrice = c.Length > 9 ? c[9].Trim() : string.Empty;

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = code;
                r["Item No.Base Price List No."] = PL;
                r["Item No, Factor"] = FX;
                r["Item No, List Price"] = listPrice;
                r["Item No, Currency for List Price"] = string.Empty;
                r["Item No, Additional Price (1)"] = string.Empty;
                r["Item No, Currency for Add. Price 1"] = string.Empty;
                r["Item No, Additional Price (2)"] = string.Empty;
                r["Item No, Currency for Add. Price 2"] = string.Empty;
                r["Item No, With UOM"] = UOM;
                r["Item No, With UOM, UOM Entry"] = UOM;
                r["Item No, With UOM, UOM Price"] = UOM;
                r["Item No, With UOM, Reduced By %"] = string.Empty;
                r["Item No, With UOM, Additional Price (1)"] = string.Empty;
                r["Item No, With UOM, Reduced By 2"] = string.Empty;
                r["Item No, With UOM, Additional Price (2)"] = string.Empty;
                r["Item No. With UOM, Reduced By %"] = string.Empty;
                dt.Rows.Add(r);
            }

            return dt;
        }

        private DataTable BuildSheetC(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var dt = new DataTable("SheetC");
            // same columns as SheetB:
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

            const string PL = "12";
            const string FX = "1";
            const string UOM = "EACH";

            // helper to parse and divide by .8
            decimal ParseAndConvert(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0m;
                // strip non-digits
                var cleaned = new string(s.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
                if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
                    return v / 0.8m;
                return 0m;
            }

            // INFO_EXCEL: D=code, K=list-price
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;
                var raw = c.Length > 10 ? c[10].Trim() : string.Empty;
                var conv = ParseAndConvert(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = code;
                r["Item No.Base Price List No."] = PL;
                r["Item No, Factor"] = FX;
                r["Item No, List Price"] = conv;
                r["Item No, Currency for List Price"] = string.Empty;
                r["Item No, Additional Price (1)"] = string.Empty;
                r["Item No, Currency for Add. Price 1"] = string.Empty;
                r["Item No, Additional Price (2)"] = string.Empty;
                r["Item No, Currency for Add. Price 2"] = string.Empty;
                r["Item No, With UOM"] = UOM;
                r["Item No, With UOM, UOM Entry"] = UOM;
                r["Item No, With UOM, UOM Price"] = UOM;
                r["Item No, With UOM, Reduced By %"] = string.Empty;
                r["Item No, With UOM, Additional Price (1)"] = string.Empty;
                r["Item No, With UOM, Reduced By 2"] = string.Empty;
                r["Item No, With UOM, Additional Price (2)"] = string.Empty;
                r["Item No. With UOM, Reduced By %"] = string.Empty;
                dt.Rows.Add(r);
            }

            // INSIDE_EXCEL: C=code, J=list-price
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;
                var raw = c.Length > 9 ? c[9].Trim() : string.Empty;
                var conv = ParseAndConvert(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = code;
                r["Item No.Base Price List No."] = PL;
                r["Item No, Factor"] = FX;
                r["Item No, List Price"] = conv;
                r["Item No, Currency for List Price"] = string.Empty;
                r["Item No, Additional Price (1)"] = string.Empty;
                r["Item No, Currency for Add. Price 1"] = string.Empty;
                r["Item No, Additional Price (2)"] = string.Empty;
                r["Item No, Currency for Add. Price 2"] = string.Empty;
                r["Item No, With UOM"] = UOM;
                r["Item No, With UOM, UOM Entry"] = UOM;
                r["Item No, With UOM, UOM Price"] = UOM;
                r["Item No, With UOM, Reduced By %"] = string.Empty;
                r["Item No, With UOM, Additional Price (1)"] = string.Empty;
                r["Item No, With UOM, Reduced By 2"] = string.Empty;
                r["Item No, With UOM, Additional Price (2)"] = string.Empty;
                r["Item No. With UOM, Reduced By %"] = string.Empty;
                dt.Rows.Add(r);
            }

            return dt;
        }
    }
}
