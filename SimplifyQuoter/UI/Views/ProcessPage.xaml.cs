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

        private int _totalCount;
        /// <summary>
        /// Total number of items to process (set when Loaded fires or when patching starts).
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount == value) return;
                _totalCount = value;
                OnPropertyChanged();                     // Notify that ProgressBar.Maximum must update
                OnPropertyChanged(nameof(PercentText));  // PercentText depends on TotalCount
            }
        }

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
        public string ConsoleText
            => string.Join(Environment.NewLine, ConsoleMessages);

        /// <summary>
        /// All items that “already existed” in SAP (queried via GET) and are queued for price patching.
        /// </summary>
        public ObservableCollection<FailedItemViewModel> FailedItems { get; } = new ObservableCollection<FailedItemViewModel>();

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            // Update ConsoleText whenever we add new console lines
            ConsoleMessages.CollectionChanged += OnConsoleMessagesChanged;

            // Hook up patch‐queue notifications as before
            FailedItems.CollectionChanged += FailedItems_CollectionChanged;

            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? "(unknown)";

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = "SM_NEW_PROD";
            else
                ServerName = "(unknown)";

            // Will set TotalCount in Loaded
            TotalCount = 0;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }


        private void OnConsoleMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Whenever ConsoleMessages changes, notify that ConsoleText has changed
            OnPropertyChanged(nameof(ConsoleText));
        }


        /// <summary>
        /// Fired once when the control appears.
        /// 1) Builds the definitive ItemDto list (Merged or new from SelectedRows).
        /// 2) Loops through each, attempting Create.  If “already exists,” GET existing prices → enqueue in FailedItems.
        /// 3) After the create-pass, shows a console summary and enables the “Patch” toolbar if there are any FailedItems.
        /// </summary>
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
                {
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    itemDtos.Add(dto);
                }
            }

            // 2) Initialize counters for creation pass
            TotalCount = itemDtos.Count;
            ProcessedCount = 0;

            var succeededItems = new List<string>();
            var outrightFailed = new List<string>();

            var itemService = new ItemService(_slClient);
            foreach (var dto in itemDtos)
            {
                string logPart = dto.FrgnName;
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
                    if (httpEx.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // 3.b) Enqueue for patching
                        try
                        {
                            var existing = await itemService.GetExistingItemAsync(dto.ItemCode);
                            var vm = new FailedItemViewModel(
                                dto.ItemCode,
                                existing.U_PurchasingPrice,
                                existing.U_SalesPrice,
                                dto.U_PurchasingPrice,
                                dto.U_SalesPrice);
                            FailedItems.Add(vm);
                            AppendConsole($"[{Timestamp}] ⚠ Item already exists: {logPart} → queued for patch");
                        }
                        catch (Exception getEx)
                        {
                            AppendConsole($"[{Timestamp}] ✘ Failed to GET existing '{logPart}': {getEx.Message}");
                            outrightFailed.Add(logPart);
                        }
                    }
                    else
                    {
                        // 3.c) Other HTTP error
                        AppendConsole($"[{Timestamp}] ✘ Error for '{logPart}': {httpEx.Message}");
                        outrightFailed.Add(logPart);
                    }
                }
                catch (Exception ex)
                {
                    // 3.d) Unexpected error
                    AppendConsole($"[{Timestamp}] ✘ Unexpected error for '{logPart}': {ex.Message}");
                    outrightFailed.Add(logPart);
                }

                ProcessedCount++;
            }

            // 4) Summarize
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
                AppendConsole($"[{Timestamp}] {outrightFailed.Count} failed (other errors):");
                outrightFailed.ForEach(c => AppendConsole($"   • {c}"));
            }
            else
            {
                AppendConsole($"[{Timestamp}] No outright failures (other than \"already exists\").");
            }

            if (FailedItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {FailedItems.Count} items already existed (queued for patch):");
                foreach (var vm in FailedItems)
                    AppendConsole($"   • {vm.ItemCode} (Old: {vm.OldPurchasingPrice}/{vm.OldSalesPrice}, New: {vm.NewPurchasingPrice}/{vm.NewSalesPrice})");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No existing items to patch.");
            }

            AppendConsole(string.Empty);

            // Enable toolbar buttons
            BtnSelectAll.IsEnabled = FailedItems.Count > 0;
            BtnDeselectAll.IsEnabled = FailedItems.Count > 0;
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
        /// so that we can detect when its IsSelectedToUpdate toggles.
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
        /// “Patch Selected” button clicked – perform PATCH for every checked item,
        /// and update the ProgressBar as each patch completes.
        /// </summary>
        private async void BtnPatchSelected_Click(object sender, RoutedEventArgs e)
        {
            var toPatch = FailedItems.Where(vm => vm.IsSelectedToUpdate).ToList();
            if (!toPatch.Any()) return;

            // ─── Reset progress indicators for the patch step ─────────────────────────
            TotalCount = toPatch.Count;   // Re-bind ProgressBar.Maximum to number of items being patched
            ProcessedCount = 0;           // Start at 0 so the bar is empty initially

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

                // ─── Increment the processed‐count so the ProgressBar moves ─────────
                ProcessedCount++;
            }

            AppendConsole($"[{Timestamp}] PATCH operation complete. {patchCount} item(s) updated.");
            RefreshPatchButtonState();

            // ─── (Optional) You could restore the original TotalCount/ProcessedCount here if you want,
            // but usually showing patch progress independently is sufficient.

            // 6) Finally, after patching, insert/update the job_log row:
            //    record total_cells, success_count, failure_count, and patch_count here.
            var state = AutomationWizardState.Current;
            int totalCells = TotalCount;                      // original number being patched
            int successCount = patchCount;
            int failureCount = totalCells - patchCount;

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
