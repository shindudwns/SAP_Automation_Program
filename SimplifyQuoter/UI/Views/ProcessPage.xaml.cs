// File: Views/ProcessPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// calls Service Layer, updates a ProgressBar & live console, and shows final completion.
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
        /// Username that’s currently logged in (for display).
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// An ObservableCollection bound to a ListBox to show live console messages.
        /// </summary>
        public ObservableCollection<string> ConsoleMessages { get; }
            = new ObservableCollection<string>();

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            // Grab SL client and username from our shared wizard state:
            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? string.Empty;

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = _slClient.HttpClient.BaseAddress.GetLeftPart(UriPartial.Authority);
            else
                ServerName = "(unknown)";

            // We'll set TotalCount in the Loaded handler (once we know which DTOs to process).
            TotalCount = 0;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        /// <summary>
        /// When this UserControl is loaded, we pick either the merged DTOs
        /// (if user clicked “Replace Excel”) or fall back to building from SelectedRows.
        /// Then we iterate over each ItemDto, call Service Layer, update a ProgressBar & console, and finish.
        /// </summary>
        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;

            // 1) Get the “merged” lists if they exist; otherwise rebuild from SelectedRows:
            var itemDtos = state.MergedItemMasterDtos;
            var quotationDtos = state.MergedQuotationDtos;

            if (itemDtos == null)
            {
                // No merged ItemMaster – rebuild from SelectedRows + user’s margin/UoM:
                double marginPct = state.MarginPercent;
                string uom = state.UoM;
                itemDtos = new System.Collections.Generic.List<ItemDto>(state.SelectedRows.Count);

                foreach (var rv in state.SelectedRows)
                {
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    itemDtos.Add(dto);
                }
            }

            if (quotationDtos == null)
            {
                // No merged Quotation – rebuild from SelectedRows:
                quotationDtos = new System.Collections.Generic.List<QuotationDto>(state.SelectedRows.Count);
                foreach (var rv in state.SelectedRows)
                {
                    var qdto = Transformer.ToQuotationDto(rv);
                    quotationDtos.Add(qdto);
                }
            }

            // 2) Now that we have definitive lists, set TotalCount & reset ProcessedCount:
            TotalCount = itemDtos.Count;
            ProcessedCount = 0;

            //
            // ─── Process “Item Master” DTOs ───────────────────────────────────────────────────
            //
            var itemService = new ItemService(_slClient);
            for (int i = 0; i < itemDtos.Count; i++)
            {
                var dto = itemDtos[i];
                string logPart = dto.FrgnName; // or dto.ItemCode
                AppendConsole($"[{Timestamp}] Processing ({i + 1}/{TotalCount}): {logPart}");

                try
                {
                    // Awaiting this call; if it throws, we catch below:
                    await itemService.CreateOrUpdateAsync(dto);
                    AppendConsole($"[{Timestamp}] ✔ Success: Item {logPart}");
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ Error: {ex.Message}");
                }

                ProcessedCount++;
            }

            //
            // ─── (Optional) If you also need to send Quotation DTOs ────────────────────────────
            //     If your flow does not include quotations, you can remove this block entirely.
            //
            var quoteService = new QuotationService(_slClient);
            for (int i = 0; i < quotationDtos.Count; i++)
            {
                var qdto = quotationDtos[i];
                AppendConsole($"[{Timestamp}] Processing Quotation ({i + 1}/{quotationDtos.Count}): {qdto.CardCode}");

                try
                {
                    await quoteService.CreateAsync(qdto);
                    AppendConsole($"[{Timestamp}] ✔ Quotation Success: {qdto.CardCode}");
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ Quotation Error: {ex.Message}");
                }

                // Note: We do NOT bump ProcessedCount again here, 
                // because ProcessedCount specifically reflects the ItemMaster loop.
            }

            AppendConsole($"[{Timestamp}] All {ProcessedCount}/{TotalCount} rows processed.");

            // 5) Finally, notify the user that we’re done
            MessageBox.Show(
                $"Item Master Data finished: {ProcessedCount}/{TotalCount} rows processed.",
                "Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        /// <summary>
        /// Returns the current time as "HH:mm:ss" for console logging.
        /// </summary>
        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        /// <summary>
        /// Add a new message to the console list (and scroll the ListBox into view).
        /// Assumes the XAML has a ListBox named "ConsoleList" bound to ConsoleMessages.
        /// </summary>
        private void AppendConsole(string message)
        {
            ConsoleMessages.Add(message);

            if (ConsoleList.Items.Count > 0)
                ConsoleList.ScrollIntoView(ConsoleList.Items[ConsoleList.Items.Count - 1]);
        }

        // ============= INotifyPropertyChanged Implementation =============
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
