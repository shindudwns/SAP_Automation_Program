using Newtonsoft.Json;

namespace SimplifyQuoter.Services.ServiceLayer.Dtos
{
    public class ItemDto
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }

        [JsonProperty("ForeignName")]
        public string FrgnName { get; set; }

        [JsonProperty("ItemsGroupCode")]
        public string ItmsGrpCod { get; set; }

        public string FirmCode { get; set; }

        public string CardCode { get; set; }

        [JsonProperty("PurchasingUoM")]
        public string BuyUnitMsr { get; set; }

        [JsonProperty("SalesUoM")]
        public string SalUnitMsr { get; set; }

        [JsonProperty("InventoryUoM")]
        public string InvntryUom { get; set; }
    }
}
