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

        // If you don’t have a preferred vendor, we’ll omit this field below
        [JsonProperty("CardCode", NullValueHandling = NullValueHandling.Ignore)]
        public string CardCode { get; set; }

        [JsonProperty("PurchasingUoM")]
        public string BuyUnitMsr { get; set; }

        [JsonProperty("SalesUoM")]
        public string SalUnitMsr { get; set; }

        [JsonProperty("InventoryUoM")]
        public string InvntryUoM { get; set; }
    }

}
