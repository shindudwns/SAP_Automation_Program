using Newtonsoft.Json;

namespace SimplifyQuoter.Services.ServiceLayer.Dtos
{
    public class ItemDto
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }

        [JsonProperty("ForeignName")]
        public string FrgnName { get; set; }

        // Must be an integer matching the SL “ItemsGroupCode”
        [JsonProperty("ItemsGroupCode")]
        public int ItmsGrpCod { get; set; }

        public string FirmCode { get; set; }

        [JsonProperty("BPCode", NullValueHandling = NullValueHandling.Ignore)]
        public string BPCode { get; set; }

        [JsonProperty("CardType", NullValueHandling = NullValueHandling.Ignore)]
        public string CardType { get; set; }

        [JsonProperty("Mainsupplier", NullValueHandling = NullValueHandling.Ignore)]
        public string Mainsupplier { get; set; }

        [JsonProperty("PurchaseUnit")]
        public string PurchaseUnit { get; set; }

        [JsonProperty("SalesUnit")]
        public string SalesUnit { get; set; }

        [JsonProperty("InventoryUOM")]
        public string InventoryUOM { get; set; }
    }

}
