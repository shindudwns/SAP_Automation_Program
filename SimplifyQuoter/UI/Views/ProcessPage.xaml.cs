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
    /// Step 4: ProcessPage loops over each selected row, calls Service Layer,
    /// updates a ProgressBar + live console, and shows final completion.
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
        /// Total number of rows to process (set in constructor).
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

            // TotalCount is simply how many rows were selected in Step 2:
            TotalCount = state.SelectedRows.Count;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        /// <summary>
        /// When this UserControl is loaded, we iterate through each selected row
        /// and call the Service Layer to create/update the item.  Progress and
        /// console messages are updated live.
        /// </summary>
        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;

            // 1) Grab margin% and UoM that the user entered in Step 2:
            double marginPct = state.MarginPercent;    // e.g. 20.0
            string uom = state.UoM;              // e.g. "EACH" or "PK"

            int idx = 0;
            foreach (var rv in state.SelectedRows)
            {
                idx++;
                // Part number or identifier for logging:
                var part = (rv.Cells.Length > 2)
                    ? rv.Cells[2]?.Trim()
                    : "<no-part>";

                AppendConsole($"[{Timestamp}] Processing ({idx}/{TotalCount}): {part}");

                try
                {
                    // 2) Build the ItemDto using marginPct & uom
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);

                    // 3) Send to Service Layer (create/update)
                    await new ItemService(_slClient).CreateOrUpdateAsync(dto);

                    // 4) Mark as processed
                    ProcessedCount++;
                    AppendConsole($"[{Timestamp}] ✔ Success: {part}");
                }
                catch (Exception ex)
                {
                    ProcessedCount++;
                    AppendConsole($"[{Timestamp}] ✘ Error: {ex.Message}");
                }
            }

            AppendConsole($"[{Timestamp}] All {ProcessedCount}/{TotalCount} rows processed.");

            // 5) Notify user with a final MessageBox
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

            // Automatic scroll into view of the last item:
            if (ConsoleList.Items.Count > 0)
                ConsoleList.ScrollIntoView(ConsoleList.Items[ConsoleList.Items.Count - 1]);
        }

        // ============= INotifyPropertyChanged Implementation =============
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
