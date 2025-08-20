// Services/DocumentGenerator.cs
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        // 클래스 상단에 추가
        private static readonly Regex KoRegex = new Regex(
            @"[\u1100-\u11FF\u3130-\u318F\uA960-\uA97F\uAC00-\uD7A3\uD7B0-\uD7FF]",
            RegexOptions.Compiled);

        /// <summary>
        /// Part Number 전용 정리:
        /// - 한글 전부 제거
        /// - 빈 괄호 (), [], {} 제거
        /// - 공백/구분자 깔끔히
        /// </summary>
        private static string CleanPartNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            // 보이지 않는 공백 정리
            s = s.Replace('\u00A0', ' ')   // NBSP
                 .Replace('\u200B', ' ')   // ZERO-WIDTH SPACE
                 .Replace('\uFEFF', ' ');  // ZERO-WIDTH NO-BREAK SPACE

            // NFKC 정규화: 풀와이드 영/숫자 → ASCII로 펴기
            var t = s.Normalize(NormalizationForm.FormKC);

            // ASCII 화이트리스트: 영/숫자/공백/일부 구분자만 허용, 나머지(한글 포함) 전부 제거
            const string allowed = "-_./,#()";
            var sb = new StringBuilder(t.Length);
            foreach (var ch in t)
            {
                if (ch <= 0x7E && (char.IsLetterOrDigit(ch) || ch == ' ' || allowed.IndexOf(ch) >= 0))
                    sb.Append(ch);
            }
            t = sb.ToString();

            // 빈 괄호 제거
            t = Regex.Replace(t, @"\(\s*\)|\[\s*\]|\{\s*\}", "");

            // 구분자/공백 정리
            t = Regex.Replace(t, @"\s*-\s*", "-");
            t = Regex.Replace(t, @"\s*,\s*", ",");
            t = Regex.Replace(t, @"-{2,}", "-");     // 연속 하이픈 압축
            t = Regex.Replace(t, @"\s{2,}", " ");

            // 앞뒤 구분자/공백 제거
            t = t.Trim(' ', ',', ';', '-');

            // (옵션) 대문자 통일
            t = t.ToUpperInvariant();

            return t;
        }

        private static string BuildDescription(params string[] pieces)
        {
            var s = string.Join(", ",
                pieces
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim().Trim(',')));

            s = Regex.Replace(s, @"\s*,\s*,\s*", ", "); // ", ," 정리
            s = Regex.Replace(s, @"\s{2,}", " ");       // 중복 공백 정리
            return s.Trim(' ', ',');                    // 앞뒤 콤마/공백 제거
        }

        private static void LogGptFromAi(AiEnrichmentService ai, string feature, string partCode)
        {
            try
            {
                var u = ai?.LastUsage;
                SimplifyQuoter.Services.Audit.LogGptUsage(
                    feature,
                    u?.PromptTokens ?? 0,
                    u?.CompletionTokens ?? 0,
                    u?.TotalTokens ?? ((u?.PromptTokens ?? 0) + (u?.CompletionTokens ?? 0)),
                    model: u?.Model ?? "unknown",
                    user: AutomationWizardState.Current?.UserName,
                    partCode: partCode,
                    items: 1
                );
            }
            catch { /* no-op */ }
        }

        // [NEW] GPT 사용 로그 헬퍼(Transformer 기반: Description/ItemGroup에 사용)
        private static void LogGptFromTransformer(string feature, string partCode)
        {
            try
            {
                SimplifyQuoter.Services.Audit.LogGptUsage(
                    feature,
                    promptTokens: 0,
                    completionTokens: 0,
                    totalTokens: 0,
                    model: "unknown",
                    user: AutomationWizardState.Current?.UserName,
                    partCode: partCode,
                    items: 1
                );
            }
            catch { /* no-op */ }
        }

        // … DocumentGenerator 클래스 안쪽 …

        // appSettings에서 정수/열문자(A, F, AA...) 모두 받아들이는 헬퍼
        private static int GetColumnIndexFromConfig(string key, int fallback)
           {
                var v = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrWhiteSpace(v)) return fallback;

                // 정수로 제공되는 경우 (0-based)
                if (int.TryParse(v, out var n) && n >= 0) return n;

                // 열 문자로 제공되는 경우 (A=0, B=1 ... AA=26)
                if (Regex.IsMatch(v.Trim(), "^[A-Za-z]+$"))
                {
                    return ColumnLettersToIndex(v.Trim());
                }

                return fallback;
           }

    private static int ColumnLettersToIndex(string col)
    {
        // A=0 기준. 엑셀 열 문자 → 0-based 인덱스
        int idx = 0;
        foreach (char ch in col.ToUpperInvariant())
        {
            idx = idx * 26 + (ch - 'A' + 1);
        }
        return idx - 1;
    }

    public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows,
            IProgress<string> log,
            IProgress<double> percent)       // <-- now double!
        {
            var sheetA = BuildSheetA(infoRows, insideRows, log, percent);
            var sheetB = BuildSheetB(infoRows, insideRows);
            var sheetC = BuildSheetC(infoRows, insideRows);

            return new List<DataTable> { sheetA, sheetB, sheetC };
        }
        // SheetA용: PART# 기준으로 최종 정리 후 Item Code 재계산
        private static void HardSanitizeSheetA(DataTable dt)
        {
            var toDelete = new List<DataRow>();

            foreach (DataRow r in dt.Rows)
            {
                var part = CleanPartNumber(Convert.ToString(r["PART#"]));
                var brand = CleanPartNumber(Convert.ToString(r["BRAND"])); // 원하면 브랜드도 강제 정리

                if (string.IsNullOrWhiteSpace(part))
                {
                    // 비어버린 행은 임포트 불가 → 삭제 예약
                    toDelete.Add(r);
                    continue;
                }

                r["PART#"] = part;
                r["BRAND"] = brand;
                r["Item Code"] = "HX-" + part;  // PART#로부터 일관 재생성
            }

            // 안전하게 루프 밖에서 삭제
            foreach (var row in toDelete)
                dt.Rows.Remove(row);
        }

        // SheetB/SheetC용: "Item No."에서 H- 제거 → 정리 → 다시 H- 부착
        private static void HardSanitizePriceTable(DataTable dt)
        {
            var toDelete = new List<DataRow>();

            foreach (DataRow r in dt.Rows)
            {
                var raw = Convert.ToString(r["Item No."]) ?? string.Empty;
                var core = raw.StartsWith("HX-", StringComparison.OrdinalIgnoreCase)
                    ? raw.Substring(2)
                    : raw;

                core = CleanPartNumber(core);

                if (string.IsNullOrWhiteSpace(core))
                {
                    toDelete.Add(r);
                    continue;
                }

                r["Item No."] = "HX-" + core;
            }

            foreach (var row in toDelete)
                dt.Rows.Remove(row);
        }
//     ^ 아이템코드 한국어 제거
        private DataTable BuildSheetA(
    IEnumerable<RowView> infoRows,
    IEnumerable<RowView> insideRows,
    IProgress<string> log,
    IProgress<double> percent)
        {
            // BuildSheetA(...) 내부
            int INFO_DESC_COL = GetColumnIndexFromConfig("Excel:InfoDescColIndex", 5);
            int INSIDE_DESC_COL = GetColumnIndexFromConfig("Excel:InsideDescColIndex", 4);

            var dt = new DataTable("SheetA");
            foreach (var name in new[]
            {
        "Item Code","PART#","BRAND","Item Group","DESCRIPTION",
        "Purchasing UOM","Sales UOM","Inventory UOM","Vendor Code"
    })
                dt.Columns.Add(name);

            Func<RowView, bool> isReady = rv =>
                rv.Cells.Length > 14 &&
                string.Equals(rv.Cells[14]?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            const string vendorCodeDefault = "VL000442";

            // 안전하게 셀 읽기
            string GetCell(string[] cells, int idx)
                => (cells != null && idx >= 0 && idx < cells.Length && cells[idx] != null)
                    ? cells[idx].Trim()
                    : string.Empty;

            // build a flat list of all READY rows for counting
            var readyList = infoRows.Concat(insideRows).Where(isReady).ToList();
            int total = readyList.Count;
            int counter = 0;

            // 한 번만 생성해서 재사용
            using (var ai = new AiEnrichmentService())
            {
                // INFO_EXCEL rows first
                foreach (var rv in infoRows.Where(isReady))
                {
                    counter++;
                    var c = rv.Cells;
                    //var code = GetCell(c, 3);
                    //var brand = GetCell(c, 2);
                    var code = CleanPartNumber(GetCell(c, 3));   // ← 한국어 제거
                    var brand = CleanPartNumber(GetCell(c, 2));


                    // 엑셀 DESCRIPTION 원문
                    var descRaw = GetCell(c, INFO_DESC_COL);

                    // 규칙:
                    // - descRaw 비어있으면: 코드 기반 기존 GPT 요약
                    // - descRaw 있으면: 한국어면 문장 제거 + 영어 키워드 추출, 영어면 그대로 정리
                    string descFinal = string.IsNullOrWhiteSpace(descRaw)
                        ? Transformer.GetDescriptionAsync(code).Result
                        : ai.ToEnglishKeywordsAsync(descRaw, uppercase: true).GetAwaiter().GetResult();

                    /* [NEW] GPT 사용 로그: DESCRIPTION/KEYWORDS */
                    if (string.IsNullOrWhiteSpace(descRaw))
                        LogGptFromTransformer("auto_description", code);  // 코드 기반 요약
                    else
                        LogGptFromAi(ai, "ai_keywords", code);            // 원문→영문 키워드 추출




                    var row = dt.NewRow();
                    row["Item Code"] = "HX-" + code;
                    row["PART#"] = code;
                    row["BRAND"] = brand;
                    row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                    /* [NEW] GPT 사용 로그: ITEM GROUP */
                    LogGptFromTransformer("auto_item_group", code);
                    row["DESCRIPTION"] = BuildDescription(brand, code, descFinal).ToUpperInvariant();
                    row["Purchasing UOM"] = "EACH";
                    row["Sales UOM"] = "EACH";
                    row["Inventory UOM"] = "EACH";
                    row["Vendor Code"] = vendorCodeDefault;
                    dt.Rows.Add(row);

                    log?.Report($"  • Building Sheet A row {counter}/{total}: " + string.Join(" | ", row.ItemArray));
                    percent?.Report(5 + 30.0 * counter / total);
                }

                // INSIDE_EXCEL rows next
                foreach (var rv in insideRows.Where(isReady))
                {
                    counter++;
                    var c = rv.Cells;
                    var code = CleanPartNumber(GetCell(c, 2));   // 한국어 제거
                    var brand = CleanPartNumber(GetCell(c, 1));
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        log?.Report($"  • Skipped row {counter}/{total}: Part Number empty after KO-strip."); // 만약 제거시 빈칸일 시, 스킵.
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    // 엑셀 DESCRIPTION 원문
                    var descRaw = GetCell(c, INSIDE_DESC_COL);

                    string descFinal = string.IsNullOrWhiteSpace(descRaw)
                        ? Transformer.GetDescriptionAsync(code).Result
                        : ai.ToEnglishKeywordsAsync(descRaw, uppercase: true).GetAwaiter().GetResult();

                    /* [NEW] GPT 사용 로그: DESCRIPTION/KEYWORDS */
                    if (string.IsNullOrWhiteSpace(descRaw))
                        LogGptFromTransformer("auto_description", code);
                    else
                        LogGptFromAi(ai, "ai_keywords", code);


                    var row = dt.NewRow();
                    row["Item Code"] = "HX-" + code;
                    row["PART#"] = code;
                    row["BRAND"] = brand;
                    row["Item Group"] = Transformer.GetItemGroupAsync(code, brand).Result;
                    /* [NEW] GPT 사용 로그: ITEM GROUP */
                    LogGptFromTransformer("auto_item_group", code);
                    row["DESCRIPTION"] = BuildDescription(brand, code, descFinal).ToUpperInvariant();
                    row["Purchasing UOM"] = "EACH";
                    row["Sales UOM"] = "EACH";
                    row["Inventory UOM"] = "EACH";
                    row["Vendor Code"] = vendorCodeDefault;
                    dt.Rows.Add(row);

                    log?.Report($"  • Building Sheet A row {counter}/{total}: " + string.Join(" | ", row.ItemArray));
                    percent?.Report(5 + 30.0 * counter / total);
                }
            }
            HardSanitizeSheetA(dt);
            return dt;
        }




        private DataTable BuildSheetB(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            var dt = new DataTable("SheetB");
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

            string OnlyNumeric(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var filtered = raw.Where(ch => char.IsDigit(ch) || ch == '.').ToArray();
                return new string(filtered);
            }

            foreach (var rv in infoRows.Where(isReady))
            {
                var c = rv.Cells;
                var codeRaw = CleanPartNumber(c.Length > 3 ? c[3].Trim() : string.Empty);
                var priceRaw = c.Length > 10 ? c[10].Trim() : string.Empty;
                var listPrice = OnlyNumeric(priceRaw);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "HX-" + codeRaw;
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

            foreach (var rv in insideRows.Where(isReady))
            {
                var c = rv.Cells;
                var codeRaw = CleanPartNumber(c.Length > 2 ? c[2].Trim() : string.Empty);
                var priceRaw = c.Length > 9 ? c[9].Trim() : string.Empty;
                var listPrice = OnlyNumeric(priceRaw);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "HX-" + codeRaw;
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
            HardSanitizePriceTable(dt);
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
                var code = CleanPartNumber(c.Length > 3 ? c[3].Trim() : string.Empty);
                var raw = c.Length > 10 ? c[10].Trim() : string.Empty;
                var conv = ParseConv(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "HX-" + code;
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
                var code = CleanPartNumber(c.Length > 2 ? c[2].Trim() : string.Empty);
                var raw = c.Length > 9 ? c[9].Trim() : string.Empty;
                var conv = ParseConv(raw).ToString(CultureInfo.InvariantCulture);

                var r = dt.NewRow();
                r["Price List No"] = PL;
                r["Item No."] = "HX-" + code;
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
            HardSanitizePriceTable(dt);
            return dt;
        }
    }
}
