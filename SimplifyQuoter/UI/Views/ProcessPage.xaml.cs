using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 4: ProcessPage
    ///  • Runs through all ItemDto objects, attempts to Create each item.
    ///  • Queues “already exists” cases into FailedItems for price patching.
    ///  • Allows the user to select which to PATCH and then issues those PATCH calls.
    ///  • Logs both passes into the job_log table.
    /// </summary>
    public partial class ProcessPage : UserControl, INotifyPropertyChanged
    {
        private readonly ServiceLayerClient _slClient;
        private string _exportTempPath;
        private const int BatchSize = 20;

        private DateTime _createStartedAt;
        private DateTime _patchStartedAt;
        private int _originalTotalCells;
        private int _processedCount;

        /// <summary>
        /// Number of items processed so far.
        /// </summary>
        public int ProcessedCount
        {
            get => _processedCount;
            set
            {
                _processedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PercentText));
            }
        }

        private int _totalCount;
        /// <summary>
        /// Total number of items to process.
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount == value) return;
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PercentText));
            }
        }



        /// <summary>
        /// “XX%” text for display over the ProgressBar.
        /// </summary>
        public string PercentText =>
            TotalCount == 0
                ? "0%"
                : $"{(ProcessedCount * 100 / TotalCount)}%";

        public string ServerName { get; private set; }
        public string UserName { get; private set; }

        public ObservableCollection<string> ConsoleMessages { get; } = new ObservableCollection<string>();
        public string ConsoleText => string.Join(Environment.NewLine, ConsoleMessages);

        public ObservableCollection<FailedItemViewModel> FailedItems { get; } = new ObservableCollection<FailedItemViewModel>();


        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;

            // Keep console in sync
            ConsoleMessages.CollectionChanged += (_, __) => OnPropertyChanged(nameof(ConsoleText));
            // Track selection for patch UI
            FailedItems.CollectionChanged += FailedItems_CollectionChanged;

            Loaded += ProcessPage_Loaded;
        }


        private void OnConsoleMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(ConsoleText));

        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        private void AppendConsole(string message)
            => ConsoleMessages.Add(message);

        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");
            var state = AutomationWizardState.Current;

            // 1) Build DTO list once
            var itemDtos = state.MergedItemMasterDtos;
            if (itemDtos == null)
            {
                itemDtos = new List<ItemDto>(state.SelectedRows.Count);
                foreach (var rv in state.SelectedRows)
                    itemDtos.Add(await Transformer.ToItemDtoAsync(rv, state.MarginPercent, state.UoM));
                state.MergedItemMasterDtos = itemDtos;
            }


            // stamp start time & initialize counters
            _createStartedAt = DateTime.Now;
            TotalCount = itemDtos.Count;
            _originalTotalCells = itemDtos.Count;
            ProcessedCount = 0;

            var succeededItems = new List<string>();
            var outrightFailed = new List<string>();
            var itemService = new ItemService(_slClient);

            // 2) Creation pass
            foreach (var dto in itemDtos)
            {
                AppendConsole($"[{Timestamp}] Processing: {dto.FrgnName}");
                try
                {
                    await itemService.CreateOrUpdateAsync(dto);
                    AppendConsole($"[{Timestamp}] ✔ Created: {dto.FrgnName}");
                    succeededItems.Add(dto.FrgnName);
                }
                catch (HttpRequestException httpEx)
                {
                    if (httpEx.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // queue for patch
                        try
                        {
                            var existing = await itemService.GetExistingItemAsync(dto.ItemCode);
                            FailedItems.Add(new FailedItemViewModel(
                                dto.ItemCode,
                                existing.U_PurchasingPrice,
                                existing.U_SalesPrice,
                                dto.U_PurchasingPrice,
                                dto.U_SalesPrice));
                            AppendConsole($"[{Timestamp}] ⚠ Exists: {dto.FrgnName} → queued for patch");
                        }
                        catch (Exception getEx)
                        {
                            AppendConsole($"[{Timestamp}] ✘ GET failed for '{dto.FrgnName}': {getEx.Message}");
                            outrightFailed.Add(dto.FrgnName);
                        }
                    }
                    else
                    {
                        AppendConsole($"[{Timestamp}] ✘ HTTP error for '{dto.FrgnName}': {httpEx.Message}");
                        outrightFailed.Add(dto.FrgnName);
                    }
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ Unexpected for '{dto.FrgnName}': {ex.Message}");
                    outrightFailed.Add(dto.FrgnName);
                }

                ProcessedCount++;
            }

            // 3) Summarize create-pass
            AppendConsole($"[{Timestamp}] All {ProcessedCount}/{TotalCount} creation attempts complete.");
            AppendConsole(string.Empty);

            if (succeededItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {succeededItems.Count} succeeded:");
                succeededItems.ForEach(c => AppendConsole($"   • {c}"));
            }
            else
            {
                AppendConsole($"[{Timestamp}] No items succeeded.");
            }

            if (outrightFailed.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {outrightFailed.Count} failed:");
                outrightFailed.ForEach(c => AppendConsole($"   • {c}"));
            }
            else
            {
                AppendConsole($"[{Timestamp}] No outright failures.");
            }

            if (FailedItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {FailedItems.Count} queued for patch:");
                foreach (var vm in FailedItems)
                    AppendConsole($"   • {vm.ItemCode}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No existing items to patch.");
            }

            AppendConsole(string.Empty);

            // 4) Insert create-pass log
            var createLog = new JobLogEntry
            {
                Id = null,
                UserId = state.UserName ?? "(unknown)",
                FileName = state.UploadedFilePath ?? "(unknown file)",
                JobType = "ItemMasterImport_Create",
                StartedAt = _createStartedAt,
                CompletedAt = DateTime.Now,
                TotalCells = _originalTotalCells,
                SuccessCount = succeededItems.Count,
                FailureCount = _originalTotalCells - succeededItems.Count,
                PatchCount = 0
            };
            using (var db = new DatabaseService())
                db.InsertJobLog(createLog);

            // 5) Enable patch UI
            BtnSelectAll.IsEnabled = FailedItems.Count > 0;
            BtnDeselectAll.IsEnabled = FailedItems.Count > 0;
            BtnPatchSelected.IsEnabled = false;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = true;
            RefreshPatchButtonState();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = false;
            RefreshPatchButtonState();
        }

        private void FailedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null) return;
            foreach (FailedItemViewModel vm in e.NewItems)
                vm.PropertyChanged += (s, ev) => RefreshPatchButtonState();
        }

        private void RefreshPatchButtonState()
                    => BtnPatchSelected.IsEnabled = FailedItems.Any(vm => vm.IsSelectedToUpdate);

        /// <summary>
        /// Patch-pass: only runs when user clicks “Patch Selected”
        /// </summary>
        private async void BtnPatchSelected_Click(object sender, RoutedEventArgs e)
        {
            var toPatch = FailedItems.Where(vm => vm.IsSelectedToUpdate).ToList();
            if (!toPatch.Any()) return;

            _patchStartedAt = DateTime.Now;
            TotalCount = toPatch.Count;
            ProcessedCount = 0;

            AppendConsole($"[{Timestamp}] Starting PATCH of {toPatch.Count} selected items...");

            var itemService = new ItemService(_slClient);
            int patchCount = 0;

            foreach (var vm in toPatch)
            {
                AppendConsole(
                    $"[{Timestamp}] Patching '{vm.ItemCode}': " +
                    $"Old({vm.OldPurchasingPrice}/{vm.OldSalesPrice}) → " +
                    $"New({vm.NewPurchasingPrice}/{vm.NewSalesPrice})"
                );
                try
                {
                    await itemService.PatchItemPricesAsync(vm.ItemCode, vm.NewPurchasingPrice, vm.NewSalesPrice);
                    AppendConsole($"[{Timestamp}] ✔ PATCH success for {vm.ItemCode}");
                    vm.IsSelectedToUpdate = false;
                    patchCount++;
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ PATCH failed for {vm.ItemCode}: {ex.Message}");
                }

                ProcessedCount++;
            }

            AppendConsole($"[{Timestamp}] PATCH operation complete. {patchCount}/{toPatch.Count} updated.");
            RefreshPatchButtonState();

            // Insert patch-pass log
            var state = AutomationWizardState.Current;
            var patchLog = new JobLogEntry
            {
                Id = null,
                UserId = state.UserName ?? "(unknown)",
                FileName = state.UploadedFilePath ?? "(unknown file)",
                JobType = "ItemMasterImport_Patch",
                StartedAt = _patchStartedAt,
                CompletedAt = DateTime.Now,
                TotalCells = toPatch.Count,
                SuccessCount = patchCount,
                FailureCount = toPatch.Count - patchCount,
                PatchCount = patchCount
            };
            using (var db = new DatabaseService())
                db.InsertJobLog(patchLog);
        }




        // ─────────────────────────────────────────────────────────
        // STEP 5: EXPORT LOGIC
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Pair a RowView with its matching ItemDto.ItemCode.
        /// </summary>
        private class RowCodePair
        {
            public RowView Row { get; set; }
            public string Code { get; set; }
        }

        /// <summary>
        /// Kick off the export: fetch data from Service Layer, build Excel.
        /// </summary>
        private async void BtnGenerateExcel_Click(object sender, RoutedEventArgs e)
        {
            BtnGenerateExcel.IsEnabled = false;
            BtnGenerateExcel.Content = "Generating…";

            var state = AutomationWizardState.Current;
            TotalCount = state.MergedItemMasterDtos.Count;
            ProcessedCount = 0;

            var pairs = state.SelectedRows
                             .Zip(state.MergedItemMasterDtos, (rv, dto) => new RowCodePair { Row = rv, Code = dto.ItemCode })
                             .ToList();

            // Build export rows (batch or parallel)
            List<FormattedExportRow> exportRows;
            if (pairs.Count <= BatchSize)
                exportRows = await FetchConcurrentlyAsync(pairs);
            else
            {
                exportRows = new List<FormattedExportRow>();
                for (int i = 0; i < pairs.Count; i += BatchSize)
                {
                    var chunk = pairs.GetRange(i, Math.Min(BatchSize, pairs.Count - i));
                    exportRows.AddRange(await FetchBatchAsync(chunk));
                }
            }

            // Write to temporary file
            _exportTempPath = Path.Combine(
                Path.GetTempPath(),
                $"SAP_Export_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            );
            await ExcelService.Instance.WriteFormattedExportAsync(exportRows, _exportTempPath);

            BtnDownloadExcel.Visibility = Visibility.Visible;
            BtnGenerateExcel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Let the user choose where to save the already-generated file.
        /// </summary>
        private void BtnDownloadExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = Path.GetFileName(_exportTempPath)
            };
            if (dlg.ShowDialog() != true) return;

            File.Copy(_exportTempPath, dlg.FileName, overwrite: true);
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }

        /// <summary>
        /// Parallel GETs (max 10 concurrent) for small sets.
        /// </summary>
        private async Task<List<FormattedExportRow>> FetchConcurrentlyAsync(List<RowCodePair> pairs)
        {
            var sem = new SemaphoreSlim(10);
            var tasks = pairs.Select(async p =>
            {
                await sem.WaitAsync();
                try { return await FetchSingleItemAsync(p.Row, p.Code); }
                finally { sem.Release(); }
            }).ToArray();

            return (await Task.WhenAll(tasks)).ToList();
        }

        /// <summary>
        /// Fetch one item’s JSON and map to a row (with fallback).
        /// </summary>
        private async Task<FormattedExportRow> FetchSingleItemAsync(RowView rv, string itemCode)
        {
            string enc = Uri.EscapeDataString(itemCode);
            string query = "$select=ItemCode,ItemName,SalesUnit,U_SalesPrice,QuantityOnStock";

            try
            {
                var resp = await _slClient.HttpClient
                    .GetAsync($"Items('{enc}')?{query}");
                if (!resp.IsSuccessStatusCode)
                {
                    AppendConsole($"[Excel] GET Items('{itemCode}') → {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return CreateEmptyRow(itemCode, rv);
                }

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                return ParseJsonToRow(json, rv);
            }
            catch (Exception ex)
            {
                AppendConsole($"[Excel] GET failed for '{itemCode}': {ex.Message}");
                return CreateEmptyRow(itemCode, rv);
            }
            finally
            {
                ProcessedCount++;
            }
        }

        /// <summary>
        /// OData $batch for larger sets.
        /// </summary>
        private async Task<List<FormattedExportRow>> FetchBatchAsync(List<RowCodePair> pairs)
        {
            var paths = pairs
                .Select(p => Uri.EscapeDataString(p.Code))
                .Distinct()
                .Select(enc => $"Items('{enc}')?$select=ItemCode,ItemName,SalesUnit,U_SalesPrice,QuantityOnStock")
                .ToList();

            string boundary = "batch_" + Guid.NewGuid().ToString("N");
            var content = ODataBatchHelper.CreateBatchContent(paths, boundary);

            var req = new HttpRequestMessage(HttpMethod.Post, "$batch") { Content = content };
            var resp = await _slClient.HttpClient.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                AppendConsole($"[Excel] $batch → {(int)resp.StatusCode} {resp.ReasonPhrase}");
                // fallback
                return await FetchConcurrentlyAsync(pairs);
            }

            var raw = await resp.Content.ReadAsStringAsync();
            var bodies = ODataBatchHelper.ParseBatchResponse(raw, boundary);

            var list = new List<FormattedExportRow>();
            int idx = 0;
            foreach (var p in pairs)
            {
                var json = JObject.Parse(bodies[idx++]);
                list.Add(ParseJsonToRow(json, p.Row));
                ProcessedCount++;
            }
            return list;
        }

        /// <summary>
        /// Map JSON + row into the export model.
        /// </summary>
        private FormattedExportRow ParseJsonToRow(JObject json, RowView rv)
        {
            string itemNo = (string)json["ItemCode"];
            string name = (string)json["ItemName"];
            string uom = (string)json["SalesUnit"];
            double price = (double?)(json["U_SalesPrice"]) ?? 0.0;

            // Stock check
            double stock = (double?)(json["QuantityOnStock"]) ?? 0.0;
            AppendConsole($"[Excel] QuantityOnStock for '{itemNo}': {stock}");

            string inStock = stock > 0
                ? stock.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

            // 2) Parse the requested quantity from column D
            double qtyWanted = 0;
            double.TryParse(rv.Cells.ElementAtOrDefault(3), out qtyWanted);

            // 3) Determine FreeText
            string defaultFreeText = Transformer.ConvertDurationToFreeText(
                rv.Cells.ElementAtOrDefault(10) ?? string.Empty
            );
            string freeText = (stock > 0 && stock >= qtyWanted)
        ? "MFG IN STOCK"
        : defaultFreeText;

            double discountPct = 0.0;  // 0% by default
            double discountFactor = 1.0 - (discountPct / 100.0);
            double lineTotal = price * qtyWanted * discountFactor;


            // 5) Build and return the row
            return new FormattedExportRow
            {
                ItemNo = itemNo,
                BPCatalogNo = string.Empty,
                ItemDescription = name,
                Quantity = qtyWanted,
                UnitPrice = price,
                // TODO:: Try with integer 0.00
                DiscountPct = discountPct,
                TaxCode = string.Empty,
                TotalLC = lineTotal,
                FreeText = freeText,
                Whse = "01",
                InStock = inStock,
                UoMName = uom,
                UoMCode = "Manual",
                Rebate = "No",
                PurchasingPrice = string.Empty,
                MarginPct = string.Empty
            };
        }

        /// <summary>
        /// Create a stub if the GET fails.
        /// </summary>
        private FormattedExportRow CreateEmptyRow(string code, RowView rv)
        {
            double qty = 0; 
            double.TryParse(rv.Cells.ElementAtOrDefault(3), out qty);
            string freeText = Transformer.ConvertDurationToFreeText(rv.Cells.ElementAtOrDefault(10) ?? "");
            double discountPct = 0.0;
            double lineTotal = 0.0 * qty * (1.0 - discountPct / 100.0);

            return new FormattedExportRow
            {
                ItemNo = code,
                BPCatalogNo = string.Empty,
                ItemDescription = "(missing)",
                Quantity = qty,
                UnitPrice = 0.0,
                DiscountPct = 0.000,
                TaxCode = string.Empty,
                TotalLC = lineTotal,
                FreeText = freeText,
                Whse = "01",
                InStock = string.Empty,
                UoMName = string.Empty,
                UoMCode = "Manual",
                Rebate = "No",
                PurchasingPrice = string.Empty,
                MarginPct = string.Empty
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));






    }




    /// <summary>
    /// VM for each “already existed” item.
    /// </summary>
    public class FailedItemViewModel : INotifyPropertyChanged
    {
        public string ItemCode { get; }
        public double OldPurchasingPrice { get; }
        public double OldSalesPrice { get; }
        public double NewPurchasingPrice { get; }
        public double NewSalesPrice { get; }

        private bool _isSelectedToUpdate;
        public bool IsSelectedToUpdate
        {
            get => _isSelectedToUpdate;
            set
            {
                if (value == _isSelectedToUpdate) return;
                _isSelectedToUpdate = value;
                OnPropertyChanged();
            }
        }

        public FailedItemViewModel(string itemCode,
                                   double oldPurch,
                                   double oldSales,
                                   double newPurch,
                                   double newSales)
        {
            ItemCode = itemCode;
            OldPurchasingPrice = oldPurch;
            OldSalesPrice = oldSales;
            NewPurchasingPrice = newPurch;
            NewSalesPrice = newSales;
            IsSelectedToUpdate = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
