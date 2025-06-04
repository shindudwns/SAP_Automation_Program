using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services.ServiceLayer.Dtos;
using SimplifyQuoter.Services;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Converts Korean durations to "WEEK ERO" and
    /// asynchronously fetches/generates part descriptions & item-groups via AI.
    /// </summary>
    public static class Transformer
    {
        public static string ConvertDurationToFreeText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var m = Regex.Match(raw.Trim(), @"^(?<n>\d+)\s*(?<unit>주|개월)$");
            if (!m.Success)
                return raw.Trim();

            int n = int.Parse(m.Groups["n"].Value);
            bool isMonth = m.Groups["unit"].Value == "개월";
            int weeks = isMonth ? n * 4 : n;
            return $"{weeks} WEEK ERO";
        }

        public static Task<string> GetDescriptionAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Task.FromResult(string.Empty);

            return GetDescriptionInternalAsync(code.Trim());
        }

        private static async Task<string> GetDescriptionInternalAsync(string code)
        {
            string desc = null;
            try
            {
                using (var db = new DatabaseService())
                    desc = db.GetDescription(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetDescription lookup failed for {code}: {ex}");
            }
            if (!string.IsNullOrEmpty(desc))
                return desc;

            try
            {
                using (var db = new DatabaseService())
                {
                    var ai = new AiEnrichmentService(db);
                    await ai.EnrichMissingAsync(new[] { code });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI enrichment (desc) failed for {code}: {ex}");
            }

            try
            {
                using (var db = new DatabaseService())
                    desc = db.GetDescription(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetDescription post-AI failed for {code}: {ex}");
            }

            return desc ?? string.Empty;
        }

        public static Task<string> GetItemGroupAsync(string code, string brand)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Task.FromResult("string.Empty");

            return GetItemGroupInternalAsync(code.Trim(), brand ?? "");
        }

        private static async Task<string> GetItemGroupInternalAsync(
            string code, string brand)
        {
            string grp = null;
            try
            {
                using (var db = new DatabaseService())
                    grp = db.GetItemGroup(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetItemGroup lookup failed for {code}: {ex}");
            }
            if (!string.IsNullOrEmpty(grp))
                return grp;

            try
            {
                using (var db = new DatabaseService())
                {
                    var ai = new AiEnrichmentService(db);
                    await ai.EnrichMissingWithContextAsync(new[]
                    {
                        new PartContext { Code = code, Brand = brand}
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI enrichment (group) failed for {code}: {ex}");
            }

            try
            {
                using (var db = new DatabaseService())
                    grp = db.GetItemGroup(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetItemGroup post-AI failed for {code}: {ex}");
            }

            return grp ?? string.Empty;
        }

        /// <summary>
        /// Map a RowView into the minimal ItemDto
        /// </summary>
        public static async Task<ItemDto> ToItemDtoAsync(
            RowView rv,
            double marginPercent,
            string uom)
        {
            // 1) Extract part code, brand, raw purchase‐price, weight
            var part = rv.Cells.Length > 2 ? rv.Cells[2]?.Trim() : string.Empty;
            var brand = rv.Cells.Length > 1 ? rv.Cells[1]?.Trim() : string.Empty;
            var price = rv.Cells.Length > 9 ? rv.Cells[9]?.Trim() : null;
            var weight = rv.Cells.Length > 11 ? rv.Cells[11]?.Trim() : null;

            Debug.WriteLine($"🔍 Raw purchase‐price cell: '{price}'");

            // 2) Clean any currency symbols or commas
            if (!string.IsNullOrEmpty(price))
                price = price.Replace("$", "").Replace(",", "");

            // 3) Parse with InvariantCulture
            double purchasePrice = 0;
            if (!string.IsNullOrEmpty(price)
                && double.TryParse(price, NumberStyles.Any,
                                   CultureInfo.InvariantCulture,
                                   out double parsed))
            {
                purchasePrice = parsed;
            }
            else
            {
                Debug.WriteLine("⚠️ Failed to parse purchase price, defaulting to 0");
            }

            // 4) Compute sales price using user’s margin%
            //    marginPercent is like 20.0 for “20%”.  We want:
            //       U_SalesPrice = purchasePrice / (1 - marginPercent/100)
            double salesPrice;
            if (marginPercent >= 100)
            {
                // guard against division by zero or negative
                salesPrice = purchasePrice;
            }
            else
            {
                double markupFactor = 1.0 - (marginPercent / 100.0);
                if (markupFactor <= 0)
                    salesPrice = purchasePrice;
                else
                    salesPrice = Math.Round(purchasePrice / markupFactor, 4);
            }

            // 5) Call AI‐enrichment as before
            using (var ai = new AiEnrichmentService())
            {
                // 5.1) Get concise summary
                var description = await ai.GeneratePartSummaryAsync(part, brand);

                // 5.2) Determine SL group code
                var groupCode = await ai.DetermineItemGroupCodeAsync(part, brand);

                // 6) Build and return the ItemDto, 
                //      setting PurchaseUnit/SalesUnit/InventoryUOM = user’s UoM
                return new ItemDto
                {
                    ItemCode = "H-" + part,
                    ItemName = brand + ", " + part + ", " + description + ", " + weight + "KG",
                    FrgnName = part,
                    ItmsGrpCod = groupCode,

                    // hard‐coded supplier info stays the same:
                    BPCode = "VL000442",
                    Mainsupplier = "VL000442",
                    CardType = "cSupplier",

                    // ↓ Set these three to the user’s UoM text:
                    PurchaseUnit = uom,
                    SalesUnit = uom,
                    InventoryUOM = uom,

                    // 7) Set purchasing price and the computed sales price:
                    U_PurchasingPrice = purchasePrice,
                    U_SalesPrice = salesPrice
                };
            }
        }



        /// <summary>
        /// Map a RowView into a single‐line QuotationDto
        /// </summary>
        public static QuotationDto ToQuotationDto(RowView rv)
        {
            var line = new QuotationLineDto
            {
                ItemCode = rv.Cells.Length > 2 ? rv.Cells[2]?.Trim() : string.Empty,
                Quantity = double.TryParse(rv.Cells.Length > 3 ? rv.Cells[3] : "0", out var q) ? q : 0,
                FreeText = ConvertDurationToFreeText(
                              rv.Cells.Length > 10 ? rv.Cells[10] : string.Empty)
            };

            return new QuotationDto
            {
                // adjust this index once you know which column is CardCode
                CardCode = rv.Cells.Length > 1 ? rv.Cells[1]?.Trim() : string.Empty,
                DocDate = DateTime.Today,
                DocumentLines = new List<QuotationLineDto> { line }
            };
        }

        /// <summary>
        /// Builds an ItemDto exactly from the cells of a “preview” worksheet.
        /// This assumes the spreadsheet columns are in the same order you use
        /// when you wrote out the preview in WriteItemMasterPreviewAsync.
        /// </summary>
        public static ItemDto ToItemDtoFromEditedRow(RowView rv, string uom)
        {
            // 1) Item No. → column 1
            string itemNo = (rv.Cells.Length > 1) ? rv.Cells[1]?.Trim() : string.Empty;

            // 2) Description → column 2
            string description = (rv.Cells.Length > 2) ? rv.Cells[2]?.Trim() : string.Empty;

            // 3) Part Number → column 3
            string partNumber = (rv.Cells.Length > 3) ? rv.Cells[3]?.Trim() : string.Empty;

            // 4) Item Group → column 4
            int itemGroup = 0;
            if (rv.Cells.Length > 4 && int.TryParse(rv.Cells[4]?.Trim(), out var ig))
            {
                itemGroup = ig;
            }

            // 5) Preferred Vendor → column 5
            string vendor = (rv.Cells.Length > 5) ? rv.Cells[5]?.Trim() : string.Empty;

            // 6) Purchasing UoM → column 6. If blank, fallback to uom.
            string purchaseUoM = (rv.Cells.Length > 6 && !string.IsNullOrWhiteSpace(rv.Cells[6]))
                                    ? rv.Cells[6].Trim()
                                    : uom;

            // 7) Sales UoM → column 7. If blank, fallback to uom.
            string salesUoM = (rv.Cells.Length > 7 && !string.IsNullOrWhiteSpace(rv.Cells[7]))
                                 ? rv.Cells[7].Trim()
                                 : uom;

            // 8) Inventory UOM → column 8. If blank, fallback to uom.
            string inventoryUoM = (rv.Cells.Length > 8 && !string.IsNullOrWhiteSpace(rv.Cells[8]))
                                      ? rv.Cells[8].Trim()
                                      : uom;

            // 9) Purchasing Price → column 9, strip commas if present
            double purchasingPrice = 0;
            if (rv.Cells.Length > 9 && !string.IsNullOrWhiteSpace(rv.Cells[9]))
            {
                var raw = rv.Cells[9].Trim().Replace(",", "");
                double.TryParse(raw,
                                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                                CultureInfo.InvariantCulture,
                                out purchasingPrice);
            }

            // 10) Sales Price → column 10, strip commas if present
            double salesPrice = 0;
            if (rv.Cells.Length > 10 && !string.IsNullOrWhiteSpace(rv.Cells[10]))
            {
                var raw = rv.Cells[10].Trim().Replace(",", "");
                double.TryParse(raw,
                                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                                CultureInfo.InvariantCulture,
                                out salesPrice);
            }

            // 11) Build and return the ItemDto
            return new ItemDto
            {
                ItemCode = itemNo,
                ItemName = description,
                FrgnName = partNumber,
                ItmsGrpCod = itemGroup,
                BPCode = vendor,
                Mainsupplier = vendor,
                PurchaseUnit = purchaseUoM,
                SalesUnit = salesUoM,
                InventoryUOM = inventoryUoM,
                U_PurchasingPrice = purchasingPrice,
                U_SalesPrice = salesPrice
            };
        }



    }

    /// <summary>
    /// Holds the extra context for item-group enrichment.
    /// </summary>
    public class PartContext
    {
        public string Code { get; set; }
        public string Brand { get; set; }
        public string Vendor { get; set; }
    }
}
