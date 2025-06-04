using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    ///  • Finally, logs everything into the job_log table (including patch_count).
    /// </summary>
    public partial class ProcessPage : UserControl, INotifyPropertyChanged
    {
        private int _processedCount;

        /// <summary>
        /// Number of items processed so far (updates the ProgressBar and PercentText).
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

        /// <summary>
        /// Total number of items to process (set when Loaded fires).
        /// </summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// “XX%” text for display over the ProgressBar.
        /// </summary>
        public string PercentText =>
            TotalCount == 0
                ? "0%"
                : $"{(ProcessedCount * 100 / TotalCount)}%";

        /// <summary>
        /// Name of the Service Layer server (e.g. “https://myserver:50000”).
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// The user who is logged in (also used as job_log.user_id).
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// Live console messages bound to an ItemsControl in XAML.
        /// </summary>
        public ObservableCollection<string> ConsoleMessages { get; } = new ObservableCollection<string>();

        /// <summary>
        /// All items that “already existed” in SAP (queried via GET) and are queued for price patching.
        /// </summary>
        public ObservableCollection<FailedItemViewModel> FailedItems { get; } = new ObservableCollection<FailedItemViewModel>();

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            //
            // ─── Listen for new items being added into FailedItems ─────────────────────────
            //
            // As soon as we add a new FailedItemViewModel to the collection (in the "already exists" branch),
            // we hook up its PropertyChanged so we can detect when its IsSelectedToUpdate toggles.
            //
            FailedItems.CollectionChanged += FailedItems_CollectionChanged;

            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? "(unknown)";

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = _slClient.HttpClient.BaseAddress.GetLeftPart(UriPartial.Authority);
            else
                ServerName = "(unknown)";

            // We will set TotalCount in ProcessPage_Loaded once we know how many items are in the list
            TotalCount = 0;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        /// <summary>
        /// Fired once when the control appears.
        /// 1) Builds the definitive ItemDto list (Merged or new from SelectedRows).
        /// 2) Loops through each, attempting Create.  If “already exists,” GET existing prices → enqueue in FailedItems.
        /// 3) After the create‐pass, shows a console summary and enables the “Patch” toolbar if there are any FailedItems.
        /// </summary>
        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;

            // 1) Build the definitive ItemDto list
            var itemDtos = state.MergedItemMasterDtos;
            if (itemDtos == null)
            {
                // No “Replace Excel” override, rebuild from SelectedRows:
                double marginPct = state.MarginPercent;
                string uom = state.UoM;
                itemDtos = new List<ItemDto>(state.SelectedRows.Count);
                foreach (var rv in state.SelectedRows)
                {
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    itemDtos.Add(dto);
                }
            }

            // 2) Initialize counters
            TotalCount = itemDtos.Count;
            ProcessedCount = 0;

            var succeededItems = new List<string>();
            var outrightFailed = new List<string>();
            // “outrightFailed” = items that failed for reasons other than “already exists”

            // 3) Attempt to CREATE each item
            var itemService = new ItemService(_slClient);
            foreach (var dto in itemDtos)
            {
                string logPart = dto.FrgnName; // or dto.ItemCode
                AppendConsole($"[{Timestamp}] Processing: {logPart}");

                try
                {
                    // 3.a) Try to POST a brand-new item
                    await itemService.CreateOrUpdateAsync(dto);
                    AppendConsole($"[{Timestamp}] ✔ Created: {logPart}");
                    succeededItems.Add(logPart);
                }
                catch (HttpRequestException httpEx)
                {
                    // 3.b) If it’s “already exists,” queue for patching:
                    if (!string.IsNullOrEmpty(httpEx.Message) &&
                        httpEx.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            // GET that item’s current prices
                            var existing = await itemService.GetExistingItemAsync(dto.ItemCode);
                            var failedVm = new FailedItemViewModel(
                                itemCode: dto.ItemCode,
                                oldPurch: existing.U_PurchasingPrice,
                                oldSales: existing.U_SalesPrice,
                                newPurch: dto.U_PurchasingPrice,
                                newSales: dto.U_SalesPrice
                            );
                            FailedItems.Add(failedVm);
                            AppendConsole($"[{Timestamp}] ⚠ Item already exists: {logPart} → queued for price patch");
                        }
                        catch (Exception getEx)
                        {
                            AppendConsole($"[{Timestamp}] ✘ Failed to GET existing '{logPart}': {getEx.Message}");
                            outrightFailed.Add(logPart);
                        }
                    }
                    else
                    {
                        // 3.c) Some other HTTP-level failure
                        AppendConsole($"[{Timestamp}] ✘ Error for '{logPart}': {httpEx.Message}");
                        outrightFailed.Add(logPart);
                    }
                }
                catch (Exception ex)
                {
                    // 3.d) Unexpected failure
                    AppendConsole($"[{Timestamp}] ✘ Unexpected error for '{logPart}': {ex.Message}");
                    outrightFailed.Add(logPart);
                }

                ProcessedCount++;
            }

            // 4) Summarize the CREATE pass in the console
            AppendConsole($"[{Timestamp}] All {ProcessedCount}/{TotalCount} creation attempts complete.");
            AppendConsole(string.Empty);

            if (succeededItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {succeededItems.Count} succeeded:");
                foreach (var code in succeededItems)
                    AppendConsole($"   • {code}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No items succeeded.");
            }

            if (outrightFailed.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {outrightFailed.Count} failed (other errors):");
                foreach (var code in outrightFailed)
                    AppendConsole($"   • {code}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No outright failures (other than \"already exists\").");
            }

            if (FailedItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {FailedItems.Count} items already existed (queued for patch):");
                foreach (var vm in FailedItems)
                    AppendConsole(
                        $"   • {vm.ItemCode} (Old: {vm.OldPurchasingPrice}/{vm.OldSalesPrice}, " +
                        $"New: {vm.NewPurchasingPrice}/{vm.NewSalesPrice})"
                    );
            }
            else
            {
                AppendConsole($"[{Timestamp}] No existing items to patch.");
            }

            AppendConsole(string.Empty);

            // 5) Enable “Select All” / “Deselect All” if we have any FailedItems
            BtnSelectAll.IsEnabled = FailedItems.Count > 0;
            BtnDeselectAll.IsEnabled = FailedItems.Count > 0;

            // “Patch Selected” remains disabled until the user actually checks at least one checkbox.
            BtnPatchSelected.IsEnabled = false;
        }

        /// <summary>
        /// “Select All” button clicked – check every FailedItemViewModel.
        /// </summary>
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = true;

            RefreshPatchButtonState();
        }

        /// <summary>
        /// “Deselect All” button clicked – uncheck every FailedItemViewModel.
        /// </summary>
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = false;

            RefreshPatchButtonState();
        }

        /// <summary>
        /// When any new FailedItemViewModel is added to the collection, subscribe to its PropertyChanged
        /// so that we can detect when IsSelectedToUpdate toggles.
        /// </summary>
        private void FailedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                {
                    if (obj is FailedItemViewModel vm)
                    {
                        vm.PropertyChanged += FailedItemViewModel_PropertyChanged;
                    }
                }
            }
        }

        /// <summary>
        /// When a single FailedItemViewModel’s PropertyChanged fires, check if it was “IsSelectedToUpdate”—
        /// and if so, update the “Patch Selected” button’s enabled state.
        /// </summary>
        private void FailedItemViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FailedItemViewModel.IsSelectedToUpdate))
            {
                RefreshPatchButtonState();
            }
        }

        /// <summary>
        /// Recalculates whether “Patch Selected” should be enabled (if at least one row is checked).
        /// </summary>
        private void RefreshPatchButtonState()
        {
            BtnPatchSelected.IsEnabled = FailedItems.Any(vm => vm.IsSelectedToUpdate);
        }

        /// <summary>
        /// “Patch Selected” button clicked – perform PATCH for every checked item.
        /// Automatically un‐checks each once done.
        /// </summary>
        private async void BtnPatchSelected_Click(object sender, RoutedEventArgs e)
        {
            var toPatch = FailedItems.Where(vm => vm.IsSelectedToUpdate).ToList();
            if (!toPatch.Any()) return;

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
            }

            AppendConsole($"[{Timestamp}] PATCH operation complete. {patchCount} item(s) updated.");
            RefreshPatchButtonState();

            // 6) Finally, after patching, insert/update the job_log row:
            //    record total_cells, success_count, failure_count, and patch_count here.
            var state = AutomationWizardState.Current;
            int totalCells = TotalCount;                      // original number of items
            int successCount = (TotalCount - FailedItems.Count) + patchCount;
            int failureCount = FailedItems.Count - patchCount;

            var jobEntry = new JobLogEntry
            {
                Id = null,
                UserId = state.UserName ?? "(unknown)",
                FileName = state.UploadedFilePath ?? "(unknown file)",
                JobType = "ItemMasterImport",
                StartedAt = DateTime.Now.AddSeconds(-ProcessedCount), // approximate
                CompletedAt = DateTime.Now,
                TotalCells = totalCells,
                SuccessCount = successCount,
                FailureCount = failureCount,
                PatchCount = patchCount
            };

            using (var db = new DatabaseService())
            {
                db.InsertJobLog(jobEntry);
            }
        }

        /// <summary>
        /// Returns “HH:mm:ss”-formatted timestamp for console logging.
        /// </summary>
        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        /// <summary>
        /// Adds a new line to the console area; the ItemsControl/ScrollViewer in XAML auto-scrolls.
        /// </summary>
        private void AppendConsole(string message)
        {
            ConsoleMessages.Add(message);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        #endregion
    }

    /// <summary>
    /// VM for each “already existed” item. Shows old vs. new prices and a checkbox.
    /// </summary>
    public class FailedItemViewModel : INotifyPropertyChanged
    {
        public string ItemCode { get; }

        /// <summary>
        /// “Old” U_PurchasingPrice from SAP.
        /// </summary>
        public double OldPurchasingPrice { get; }

        /// <summary>
        /// “Old” U_SalesPrice from SAP.
        /// </summary>
        public double OldSalesPrice { get; }

        /// <summary>
        /// “New” U_PurchasingPrice that the user attempted.
        /// </summary>
        public double NewPurchasingPrice { get; }

        /// <summary>
        /// “New” U_SalesPrice that the user attempted.
        /// </summary>
        public double NewSalesPrice { get; }

        private bool _isSelectedToUpdate;
        /// <summary>
        /// Whether the user has checked this row for patching (“Patch Selected”).
        /// </summary>
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
        private void OnPropertyChanged([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
