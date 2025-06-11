using System;

namespace SimplifyQuoter.Models
{
    public class FormattedExportRow
    {
        public string ItemNo { get; set; }
        public string BPCatalogNo { get; set; }
        public string ItemDescription { get; set; }
        public double Quantity { get; set; }
        public double UnitPrice { get; set; }
        public string DiscountPct { get; set; }
        public string TaxCode { get; set; }
        public double TotalLC { get; set; }
        public string FreeText { get; set; }
        public string Whse { get; set; }
        public string InStock { get; set; }
        public string UoMName { get; set; }
        public string UoMCode { get; set; }
        public string Rebate { get; set; }
        public string PurchasingPrice { get; set; }
        public string MarginPct { get; set; }
    }
}
