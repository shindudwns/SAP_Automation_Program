using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            ConsoleMessages.CollectionChanged += OnConsoleMessagesChanged;
            FailedItems.CollectionChanged += FailedItems_CollectionChanged;

            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? "(unknown)";
            ServerName = _slClient?.HttpClient?.BaseAddress != null
                        ? "SM_NEW_PROD"
                        : "(unknown)";

            TotalCount = 0;
            ProcessedCount = 0;

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

            // 1) Build the definitive ItemDto list
            var itemDtos = state.MergedItemMasterDtos;
            if (itemDtos == null)
            {
                double marginPct = state.MarginPercent;
                string uom = state.UoM;
                itemDtos = new List<ItemDto>(state.SelectedRows.Count);
                foreach (var rv in state.SelectedRows)
                    itemDtos.Add(await Transformer.ToItemDtoAsync(rv, marginPct, uom));
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
            foreach (var obj in e.NewItems.OfType<FailedItemViewModel>())
                obj.PropertyChanged += FailedItemViewModel_PropertyChanged;
        }

        private void FailedItemViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FailedItemViewModel.IsSelectedToUpdate))
                RefreshPatchButtonState();
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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        #endregion
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
