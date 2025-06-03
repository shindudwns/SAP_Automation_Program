// File: Views/ProcessPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 4: ProcessPage loops over each selected row, calls Service Layer,
    /// updates a ProgressBar + live console, and shows final completion.
    /// </summary>
    public partial class ProcessPage : UserControl, INotifyPropertyChanged
    {
        private int _processedCount;

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

        public int TotalCount { get; private set; }
        public string PercentText => $"{(TotalCount == 0 ? 0 : (ProcessedCount * 100 / TotalCount))}%";

        public string ServerName { get; private set; }
        public string UserName { get; private set; }

        public ObservableCollection<string> ConsoleMessages { get; } = new ObservableCollection<string>();

        private readonly ServiceLayerClient _slClient;

        public ProcessPage()
        {
            InitializeComponent();
            DataContext = this;

            var state = AutomationWizardState.Current;
            _slClient = state.SlClient;
            UserName = state.UserName ?? string.Empty;

            if (_slClient?.HttpClient?.BaseAddress != null)
                ServerName = _slClient.HttpClient.BaseAddress.GetLeftPart(UriPartial.Authority);
            else
                ServerName = "(unknown)";

            TotalCount = state.SelectedRows.Count;
            ProcessedCount = 0;

            Loaded += ProcessPage_Loaded;
        }

        private async void ProcessPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppendConsole($"[{Timestamp}] Starting Item Master Data processing...");

            var state = AutomationWizardState.Current;
            int idx = 0;

            foreach (var rv in state.SelectedRows)
            {
                idx++;
                var part = rv.Cells.Length > 2 ? rv.Cells[2].Trim() : "<no-part>";
                AppendConsole($"[{Timestamp}] Processing ({idx}/{TotalCount}): {part}");

                try
                {
                    var dto = await Transformer.ToItemDtoAsync(rv);
                    await new ItemService(_slClient).CreateOrUpdateAsync(dto);

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
            MessageBox.Show($"Item Master Data finished: {ProcessedCount}/{TotalCount} rows processed.",
                            "Completed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        private string Timestamp => DateTime.Now.ToString("HH:mm:ss");

        private void AppendConsole(string message)
        {
            ConsoleMessages.Add(message);
            // Auto-scroll in ListBox
            if (ConsoleList.Items.Count > 0)
                ConsoleList.ScrollIntoView(ConsoleList.Items[ConsoleList.Items.Count - 1]);
        }

        // INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
