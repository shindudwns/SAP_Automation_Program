// File: Services/AutomationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services.ServiceLayer;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Orchestrates Item & Quotation creation via SL client + feature services,
    /// and tracks progress in local DB (process_job & sap_row flags).
    /// </summary>
    public class AutomationService
    {
        private readonly Guid _sapFileId;
        private readonly ItemService _itemService;
        private readonly QuotationService _quoteService;

        public AutomationService(
            Guid sapFileId,
            ItemService itemService,
            QuotationService quoteService)
        {
            _sapFileId = sapFileId;
            _itemService = itemService;
            _quoteService = quoteService;
        }

        public async Task RunItemMasterDataAsync(IEnumerable<RowView> rows)
        {
            // materialize for Count() and stable ordering
            var list = rows.ToList();

            // 1) create process_job
            Guid jobId;
            using (var db = new DatabaseService())
            {
                jobId = db.CreateProcessJob(_sapFileId, "IMD", list.Count);
            }

            // 2) loop each row
            foreach (var rv in list)
            {
                // call SL
                var dto = await Transformer.ToItemDtoAsync(rv);
                await _itemService.CreateOrUpdateAsync(dto);

                // mark processed and bump counters
                using (var db = new DatabaseService())
                {
                    db.MarkImdProcessed(rv.RowId);
                    db.IncrementJobProgress(jobId);
                }
            }

            // 3) complete job
            using (var db = new DatabaseService())
            {
                db.CompleteJob(jobId);
            }
        }

        public async Task RunSalesQuotationAsync(IEnumerable<RowView> rows)
        {
            var list = rows.ToList();

            // 1) create process_job
            Guid jobId;
            using (var db = new DatabaseService())
            {
                jobId = db.CreateProcessJob(_sapFileId, "SQ", list.Count);
            }

            // 2) loop each row
            foreach (var rv in list)
            {
                // call SL
                var dto = Transformer.ToQuotationDto(rv);
                await _quoteService.CreateAsync(dto);

                // mark processed and bump counters
                using (var db = new DatabaseService())
                {
                    db.MarkSqProcessed(rv.RowId);
                    db.IncrementJobProgress(jobId);
                }
            }

            // 3) complete job
            using (var db = new DatabaseService())
            {
                db.CompleteJob(jobId);
            }
        }
    }
}
