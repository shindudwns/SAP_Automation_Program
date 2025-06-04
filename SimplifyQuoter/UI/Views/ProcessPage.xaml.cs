// File: Views/ProcessPage.xaml.cs
using System;
using System.Collections.Generic;
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
    /// calls Service Layer, updates a live console, and finally logs the job.
    /// </summary>
    public partial class ProcessPage : UserControl, INotifyPropertyChanged
    {
        private int _processedCount;

        /// <summary>
        /// Number of rows processed so far (updates the progress bar, if bound).
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

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            // Grab the SL client and current user from our shared wizard state:
            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? string.Empty;

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = _slClient.HttpClient.BaseAddress.GetLeftPart(UriPartial.Authority);
            else
                ServerName = "(unknown)";

            // We will set TotalCount in the Loaded handler once we know which DTOs to process.
            TotalCount = 0;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        /// <summary>
        /// When this UserControl is loaded, we pick either the merged DTOs (if “Replace Excel” was used)
        /// or rebuild fresh from SelectedRows.  Then we loop over each ItemDto, call Service Layer,
        /// update a live console, show a summary of successes/failures, and insert a single row into job_log.
        /// </summary>
        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;

            // 1) Determine the definitive list of ItemDto to process:
            var itemDtos = state.MergedItemMasterDtos;
            if (itemDtos == null)
            {
                // Rebuild from SelectedRows + margin/UoM:
                double marginPct = state.MarginPercent;
                string uom = state.UoM;
                itemDtos = new List<ItemDto>(state.SelectedRows.Count);
                foreach (var rv in state.SelectedRows)
                {
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    itemDtos.Add(dto);
                }
            }

            // 2) Set up counters and totals
            TotalCount = itemDtos.Count;
            ProcessedCount = 0;

            var succeededItems = new List<string>();
            var failedItems = new List<string>();

            // 3) Loop through each ItemDto and call the Service Layer
            var itemService = new ItemService(_slClient);
            for (int i = 0; i < itemDtos.Count; i++)
            {
                var dto = itemDtos[i];
                string logPart = dto.FrgnName; // or dto.ItemCode
                AppendConsole($"[{Timestamp}] Processing ({i + 1}/{TotalCount}): {logPart}");

                try
                {
                    await itemService.CreateOrUpdateAsync(dto);
                    AppendConsole($"[{Timestamp}] ✔ Success: Item {logPart}");
                    succeededItems.Add(logPart);
                }
                catch (Exception ex)
                {
                    AppendConsole($"[{Timestamp}] ✘ Error: {ex.Message}");
                    failedItems.Add(logPart);
                }

                ProcessedCount++;
            }

            // 4) When done, print a final “All done” line in the console
            AppendConsole($"[{Timestamp}] All {ProcessedCount}/{TotalCount} rows processed.");
            AppendConsole(string.Empty);

            // 5) Print a summary of which items succeeded and which failed
            if (succeededItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {succeededItems.Count} succeeded:");
                foreach (var part in succeededItems)
                    AppendConsole($"   • {part}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No items succeeded.");
            }

            if (failedItems.Count > 0)
            {
                AppendConsole($"[{Timestamp}] {failedItems.Count} failed:");
                foreach (var part in failedItems)
                    AppendConsole($"   • {part}");
            }
            else
            {
                AppendConsole($"[{Timestamp}] No items failed.");
            }

            AppendConsole(string.Empty);

            // 6) Finally—write a single row into job_log so we can track this run
            var jobEntry = new JobLogEntry
            {
                // Let DB generate its own UUID if we pass null here:
                Id = null,
                UserId = state.UserName ?? "UNKNOWN_USER",
                FileName = state.UploadedFilePath ?? "UNKNOWN_FILE",
                JobType = "ItemMasterImport",
                StartedAt = DateTime.Now.AddSeconds(-ProcessedCount), // approximate
                CompletedAt = DateTime.Now,
                TotalCells = TotalCount,
                SuccessCount = succeededItems.Count,
                FailureCount = failedItems.Count
            };

            using (var db = new DatabaseService())
            {
                db.InsertJobLog(jobEntry);
            }

            // That’s it—do NOT pop up a MessageBox (we already wrote everything into the console).
        }

        /// <summary>
        /// Returns the current time as "HH:mm:ss" for console logging.
        /// </summary>
        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        /// <summary>
        /// Appends a line of text into the console area.
        /// The new XAML for ProcessPage will bind an ItemsControl/ScrollViewer to this collection
        /// so that it auto-scrolls as new lines appear.
        /// </summary>
        private void AppendConsole(string message)
        {
            ConsoleMessages.Add(message);
            // We rely on the ItemsControl’s ScrollViewer in XAML to auto-scroll. 
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
