using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

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
        public static ItemDto ToItemDto(RowView rv)
        {
            var part = rv.Cells.Length > 2
                       ? rv.Cells[2]?.Trim()
                       : string.Empty;

            return new ItemDto
            {
                ItemCode = part,
                ItemName = "TEST22222" /* TODO: call GetDescriptionAsync(part) and await */,
                FrgnName = "H-"+part,
                ItmsGrpCod = 100 /* TODO: call GetItemGroupAsync(part, brand) */,
                //CardCode = "VL000442",
                //BuyUnitMsr = "EACH",
                //SalUnitMsr = "EACH",
                //InvntryUom = "EACH"
            };

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
