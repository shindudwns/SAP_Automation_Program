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

        [JsonProperty("CardCode", NullValueHandling = NullValueHandling.Ignore)]
        public string CardCode { get; set; }

        [JsonProperty("CardType", NullValueHandling = NullValueHandling.Ignore)]
        public string CardType { get; set; }


        [JsonProperty("PurchasingUoM")]
        public string PurchasingUoM { get; set; }

        [JsonProperty("SalesUoM")]
        public string SalesUoM { get; set; }

        [JsonProperty("InventoryUoM", NullValueHandling = NullValueHandling.Ignore)]
        public string InventoryUoM { get; set; }
    }

}
