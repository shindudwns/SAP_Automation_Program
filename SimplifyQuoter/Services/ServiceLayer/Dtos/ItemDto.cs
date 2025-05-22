// File: Services/ServiceLayer/Dtos/ItemDto.cs
namespace SimplifyQuoter.Services.ServiceLayer.Dtos
{
    /// <summary>
    /// Minimal payload for POST /b1s/v1/Items
    /// </summary>
    public class ItemDto
    {
        /// <summary>
        /// maps to OITM.ItemCode
        /// </summary>
        public string ItemCode { get; set; }

        /// <summary>
        /// maps to OITM.ItemName
        /// </summary>
        public string ItemName { get; set; }

        // TODO: add other SL fields as needed (e.g. ItemsGroupCode, UoMEntry, etc.)
    }
}
