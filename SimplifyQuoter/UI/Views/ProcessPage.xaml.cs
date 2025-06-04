// File: Views/ProcessPage.xaml.cs
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
    /// Step 4: ProcessPage loops over each (possibly replaced) ItemDto,
    /// calls Service Layer, updates a live console, shows before/after failures,
    /// and finally logs the job into job_log.
    /// </summary>
    public partial class ProcessPage : UserControl, INotifyPropertyChanged
    {
        private int _processedCount;

        /// <summary>
        /// Number of rows processed so far (updates the progress bar).
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
        /// Total number of rows to process (set in Loaded).
        /// </summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// A simple "XX%" text for display (e.g. "50%").
        /// </summary>
        public string PercentText =>
            TotalCount == 0
                ? "0%"
                : $"{(ProcessedCount * 100 / TotalCount)}%";

        /// <summary>
        /// Name of the Service Layer server (e.g. "https://myserver:50000").
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// Username that’s currently logged in (for display and for job_log.user_id).
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// An ObservableCollection bound to an ItemsControl (or ListBox) to show live console messages.
        /// </summary>
        public ObservableCollection<string> ConsoleMessages { get; }
            = new ObservableCollection<string>();

        /// <summary>
        /// Collection of items that “already existed” and need price‐patching.
        /// </summary>
        public ObservableCollection<FailedItemViewModel> FailedItems { get; }
            = new ObservableCollection<FailedItemViewModel>();

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            // Grab SL client and current user from shared wizard state:
            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? "(unknown)";

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = _slClient.HttpClient.BaseAddress.GetLeftPart(UriPartial.Authority);
            else
                ServerName = "(unknown)";

            // We’ll set TotalCount in Loaded, once we know which DTOs to process
            TotalCount = 0;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        /// <summary>
        /// Called once when the UserControl is first shown.
        /// 1) Attempts a Create for each ItemDto.
        /// 2) If “already exists”, loads the existing prices and adds to FailedItems.
        /// 3) At the end, prints a summary in Console and populates FailedItemsDataGrid.
        /// 4) Writes a single row into job_log.
        /// </summary>
        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;

            // 1) Build the definitive list of ItemDto to process:
            var itemDtos = state.MergedItemMasterDtos;
            if (itemDtos == null)
            {
                // No “Replace Excel” override – rebuild from SelectedRows:
                double marginPct = state.MarginPercent;
                string uom = state.UoM;
                itemDtos = new List<ItemDto>(state.SelectedRows.Count);

                foreach (var rv in state.SelectedRows)
                {
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    itemDtos.Add(dto);
                }
            }

            // 2) Initialize counters and temporary lists
            TotalCount = itemDtos.Count;
            ProcessedCount = 0;

            var succeededItems = new List<string>();
            var outrightFailed = new List<string>();
            // “outrightFailed” = those that failed for reasons other than “already exists”

            // 3) First pass – attempt to Create each item
            var itemService = new ItemService(_slClient);
            foreach (var dto in itemDtos)
            {
                string logPart = dto.FrgnName; // or dto.ItemCode
                AppendConsole($"[{Timestamp}] Processing: {logPart}");

                try
                {
                    await itemService.CreateOrUpdateAsync(dto);
                    AppendConsole($"[{Timestamp}] ✔ Success: {logPart}");
                    succeededItems.Add(logPart);
                }
                catch (HttpRequestException httpEx)
                {
                    // Check if this was a “400 … already exists” error:
                    if (httpEx.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Fetch the existing item’s prices:
                        try
                        {
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
                            // Could not fetch existing data – treat as an outright failure
                            AppendConsole($"[{Timestamp}] ✘ Could not retrieve existing item {logPart}: {getEx.Message}");
                            outrightFailed.Add(logPart);
                        }
                    }
                    else
                    {
                        // Some other 400/500 error
                        AppendConsole($"[{Timestamp}] ✘ Error ({logPart}): {httpEx.Message}");
                        outrightFailed.Add(logPart);
                    }
                }
                catch (Exception ex)
                {
                    // Unexpected exception
                    AppendConsole($"[{Timestamp}] ✘ Unexpected Exception ({logPart}): {ex.Message}");
                    outrightFailed.Add(logPart);
                }

                ProcessedCount++;
            }

            // 4) Final summary of the “Create” loop
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
                AppendConsole($"[{Timestamp}] No outright failures (other than “already exists”).");
            }

            if (FailedItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {FailedItems.Count} items already existed (queued for patch):");
                foreach (var vm in FailedItems)
                    AppendConsole($"   • {vm.ItemCode}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No existing items to patch.");
            }

            AppendConsole(string.Empty);

            // 5) Enable/disable “Select All” / “Deselect All” / “Patch Selected” buttons
            BtnSelectAll.IsEnabled = FailedItems.Count > 0;
            BtnDeselectAll.IsEnabled = FailedItems.Count > 0;
            BtnPatchSelected.IsEnabled = false;
            // → “Patch Selected” remains disabled until the user checks at least one row
        }

        /// <summary>
        /// “Select All” clicked – check every FailedItemViewModel.
        /// </summary>
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = true;

            BtnPatchSelected.IsEnabled = FailedItems.Any(x => x.IsSelectedToUpdate);
        }

        /// <summary>
        /// “Deselect All” clicked – uncheck every FailedItemViewModel.
        /// </summary>
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in FailedItems)
                vm.IsSelectedToUpdate = false;

            BtnPatchSelected.IsEnabled = false;
        }

        /// <summary>
        /// Fires whenever the user toggles a cell in FailedItemsDataGrid.
        /// We simply check “is at least one row IsSelectedToUpdate == true?” to enable Patch button.
        /// </summary>
        private void FailedItemsDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            BtnPatchSelected.IsEnabled = FailedItems.Any(x => x.IsSelectedToUpdate);
        }

        /// <summary>
        /// “Patch Selected” clicked – PATCH prices for each checked item.
        /// </summary>
        private async void BtnPatchSelected_Click(object sender, RoutedEventArgs e)
        {
            var toPatch = FailedItems.Where(x => x.IsSelectedToUpdate).ToList();
            if (!toPatch.Any()) return;

            var itemService = new ItemService(_slClient);
            AppendConsole($"[{Timestamp}] Starting PATCH of {toPatch.Count} selected items...");

            foreach (var vm in toPatch)
            {
                AppendConsole($"[{Timestamp}] Patching {vm.ItemCode}: Old({vm.OldPurchasingPrice}/{vm.OldSalesPrice}) → New({vm.NewPurchasingPrice}/{vm.NewSalesPrice})");
                try
                {
                    await itemService.PatchItemPricesAsync(vm.ItemCode, vm.NewPurchasingPrice, vm.NewSalesPrice);
                    AppendConsole($"[{Timestamp}] ✔ PATCH success for {vm.ItemCode}");
                    vm.IsSelectedToUpdate = false;
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ PATCH failed for {vm.ItemCode}: {ex.Message}");
                }
            }

            AppendConsole($"[{Timestamp}] PATCH operation complete.");
            BtnPatchSelected.IsEnabled = false;
        }

        /// <summary>
        /// Returns the current time as "HH:mm:ss" for console logging.
        /// </summary>
        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        /// <summary>
        /// Appends a line of text into the console area.
        /// The ItemsControl + ScrollViewer in XAML will auto‐scroll.
        /// </summary>
        private void AppendConsole(string message)
        {
            ConsoleMessages.Add(message);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }


    /// <summary>
    /// ViewModel for each “already existed” item, showing old vs. new prices and a checkbox.
    /// </summary>
    public class FailedItemViewModel : INotifyPropertyChanged
    {
        public string ItemCode { get; }

        /// <summary>
        /// The “old” U_PurchasingPrice currently in SAP.
        /// </summary>
        public double OldPurchasingPrice { get; }

        /// <summary>
        /// The “old” U_SalesPrice currently in SAP.
        /// </summary>
        public double OldSalesPrice { get; }

        /// <summary>
        /// The “new” U_PurchasingPrice the user attempted to push.
        /// </summary>
        public double NewPurchasingPrice { get; }

        /// <summary>
        /// The “new” U_SalesPrice the user attempted to push.
        /// </summary>
        public double NewSalesPrice { get; }

        private bool _isSelectedToUpdate;
        /// <summary>
        /// Whether the user has checked this row for patching via “Patch Selected.”
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

        public FailedItemViewModel(string itemCode, double oldPurch, double oldSales, double newPurch, double newSales)
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
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
