// File: Services/ServiceLayer/Dtos/ItemDto.cs
namespace SimplifyQuoter.Services.ServiceLayer.Dtos
{
    /// <summary>
    /// Payload for POST /b1s/v1/Items (OITM).
    /// </summary>
    public class ItemDto
    {
        /// <summary>OITM.ItemCode ← Excel Col C</summary>
        public string ItemCode { get; set; }

        /// <summary>OITM.ItemName ← use AI (TODO)</summary>
        public string ItemName { get; set; }

        /// <summary>OITM.FrgnName ← Excel Col C (Part Number)</summary>
        public string FrgnName { get; set; }

        /// <summary>OITM.ItmsGrpCod ← use AI (TODO)</summary>
        public string ItmsGrpCod { get; set; }

        /// <summary>OITM.CardCode ← Preferred Vendor (static)</summary>
        public string CardCode { get; set; }

        /// <summary>OITM.BuyUnitMsr ← Purchasing UoM (static)</summary>
        public string BuyUnitMsr { get; set; }

        /// <summary>OITM.SalUnitMsr ← Sales UoM (static)</summary>
        public string SalUnitMsr { get; set; }

        /// <summary>OITM.InvntryUom ← Inventory UoM (static)</summary>
        public string InvntryUom { get; set; }
    }
}
