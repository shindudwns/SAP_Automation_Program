using System.Collections.ObjectModel;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class ImportWindow : Window
    {
        private ObservableCollection<RowView> _infoRows;
        private ObservableCollection<RowView> _insideRows;

        public ImportWindow()
        {
            InitializeComponent();
        }

        private void BtnUploadInfo_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            _infoRows = svc.LoadSheetViaDialog();
            MessageBox.Show(_infoRows != null
                ? $"INFO_EXCEL loaded: {_infoRows.Count} rows"
                : "No file loaded.");
        }

        private void BtnUploadInside_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            _insideRows = svc.LoadSheetViaDialog();
            MessageBox.Show(_insideRows != null
                ? $"INSIDE_EXCEL loaded: {_insideRows.Count} rows"
                : "No file loaded.");
        }

        private void BtnProcessImport_Click(object sender, RoutedEventArgs e)
        {
            if ((_infoRows == null || _infoRows.Count == 0) &&
                (_insideRows == null || _insideRows.Count == 0))
            {
                MessageBox.Show("Please upload at least one Excel file.");
                return;
            }

            // TODO: filter READY rows & generate three sheets
            MessageBox.Show("Will be implemented");

            // Enable download buttons
            BtnDownloadA.IsEnabled = true;
            BtnDownloadB.IsEnabled = true;
            BtnDownloadC.IsEnabled = true;
        }

        private void BtnDownloadA_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Will be implemented");
        }

        private void BtnDownloadB_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Will be implemented");
        }

        private void BtnDownloadC_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Will be implemented");
        }
    }
}
