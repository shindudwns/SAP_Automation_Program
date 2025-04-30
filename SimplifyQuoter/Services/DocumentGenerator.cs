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
    ///  - SheetB builds your price-list data (PL=11)
    ///  - SheetC builds your alternate price-list (PL=12, ÷0.8)
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
            // Add SheetA columns
            foreach (var name in new[]
            {
                "Item Code",
                "PART#",
                "BRAND",
                "Item Group",
                "DESCRIPTION",
                "Purchasing UOM",
                "Sales UOM",
                "Inventory UOM",
                "Vendor Code"
            })
            {
                dt.Columns.Add(name);
            }

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);
            const string vendorCodeDefault = "VL000442";

            // INFO_EXCEL rows
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;  // D
                var brand = c.Length > 2 ? c[2].Trim() : string.Empty;  // C

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                row["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = vendorCodeDefault;
                dt.Rows.Add(row);
            }

            // INSIDE_EXCEL rows
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 2 ? c[2].Trim() : string.Empty;  // C
                var brand = c.Length > 1 ? c[1].Trim() : string.Empty;  // B

                var row = dt.NewRow();
                row["Item Code"] = "H-" + code;
                row["PART#"] = code;
                row["BRAND"] = brand;
                row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                row["DESCRIPTION"] = Transformer.GetDescriptionAsync(code).Result;
                row["Purchasing UOM"] = "EACH";
                row["Sales UOM"] = "EACH";
                row["Inventory UOM"] = "EACH";
                row["Vendor Code"] = vendorCodeDefault;
                dt.Rows.Add(row);
            }

            return dt;
        }

        private DataTable BuildSheetB(
    IEnumerable<RowView> infoRows,
    IEnumerable<RowView> insideRows)
        {
            var dt = new DataTable("SheetB");
            // Add your columns exactly once:
            foreach (var name in new[]
            {
                "Price List No",
                "Item No.",
                "Item No.Base Price List No.",
                "Item No, Factor",
                "Item No, List Price",
                "Item No, Currency for List Price",
                "Item No, Additional Price (1)",
                "Item No, Currency for Add. Price 1",
                "Item No, Additional Price (2)",
                "Item No, Currency for Add. Price 2",
                "Item No, With UOM",
                "Item No, With UOM, UOM Entry",
                "Item No, With UOM, UOM Price",
                "Item No, With UOM, Reduced By %",
                "Item No, With UOM, Additional Price (1)",
                "Item No, With UOM, Reduced By 2",
                "Item No, With UOM, Additional Price (2)",
                "Item No. With UOM, Reduced By %"
            })
                dt.Columns.Add(name);

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            const string PL = "11";
            const string FX = "1";
            const string UOM = "EACH";

            // Helper that removes everything but digits and dot
            string OnlyNumeric(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var filtered = raw.Where(ch => char.IsDigit(ch) || ch == '.').ToArray();
                return new string(filtered);
            }

            // INFO_EXCEL rows (Column D = idx 3, List-Price Column K = idx 10)
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var codeRaw = c.Length > 3 ? c[3].Trim() : string.Empty;
                var priceRaw = c.Length > 10 ? c[10].Trim() : string.Empty;
                var listPrice = OnlyNumeric(priceRaw);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "H-" + codeRaw;
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

            // INSIDE_EXCEL rows (Column C = idx 2, List-Price Column J = idx 9)
            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var codeRaw = c.Length > 2 ? c[2].Trim() : string.Empty;
                var priceRaw = c.Length > 9 ? c[9].Trim() : string.Empty;
                var listPrice = OnlyNumeric(priceRaw);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "H-" + codeRaw;
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
            // Add the exact same columns as SheetB
            foreach (var name in new[]
            {
                "Price List No",
                "Item No.",
                "Item No.Base Price List No.",
                "Item No, Factor",
                "Item No, List Price",
                "Item No, Currency for List Price",
                "Item No, Additional Price (1)",
                "Item No, Currency for Add. Price 1",
                "Item No, Additional Price (2)",
                "Item No, Currency for Add. Price 2",
                "Item No, With UOM",
                "Item No, With UOM, UOM Entry",
                "Item No, With UOM, UOM Price",
                "Item No, With UOM, Reduced By %",
                "Item No, With UOM, Additional Price (1)",
                "Item No, With UOM, Reduced By 2",
                "Item No, With UOM, Additional Price (2)",
                "Item No. With UOM, Reduced By %"
            })
            {
                dt.Columns.Add(name);
            }

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            const string PL = "12";
            const string FX = "1";
            const string UOM = "EACH";

            // helper to parse a string price and divide by .8
            decimal ParseConv(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0m;
                // strip out digits and dot
                var cleaned = new string(s.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
                decimal v;
                if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v))
                    return v / 0.8m;
                return 0m;
            }

            // INFO_EXCEL: D=code, K=list-price
            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var code = c.Length > 3 ? c[3].Trim() : string.Empty;
                var raw = c.Length > 10 ? c[10].Trim() : string.Empty;
                var conv = ParseConv(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "H-" + code;
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
                var conv = ParseConv(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "H-" + code;
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
