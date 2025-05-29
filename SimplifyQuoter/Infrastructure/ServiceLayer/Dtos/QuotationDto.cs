// File: Services/ServiceLayer/Dtos/QuotationDto.cs
using System;
using System.Collections.Generic;

namespace SimplifyQuoter.Services.ServiceLayer.Dtos
{
    /// <summary>
    /// Line‐level DTO for Sales Quotation
    /// </summary>
    public class QuotationLineDto
    {
        /// <summary>OQUT.ItemCode</summary>
        public string ItemCode { get; set; }

        /// <summary>OQUT.Quantity</summary>
        public double Quantity { get; set; }

        /// <summary>UDF or Free text field</summary>
        public string FreeText { get; set; }
    }

    /// <summary>
    /// Payload for POST /b1s/v1/SalesQuotations
    /// </summary>
    public class QuotationDto
    {
        /// <summary>CardCode (customer) for the quotation</summary>
        public string CardCode { get; set; }

        /// <summary>Document date</summary>
        public DateTime DocDate { get; set; }

        /// <summary>Lines in the quotation</summary>
        public List<QuotationLineDto> DocumentLines { get; set; }
    }
}
