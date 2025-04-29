// Views/ImportWindow.xaml.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class ImportWindow : Window
    {
        private Guid _importFileId;
        private ObservableCollection<RowView> _infoRows;
        private ObservableCollection<RowView> _insideRows;
        private List<string> _txtPaths;
        private readonly ImportTxtService _importSvc;

        public ImportWindow()
        {
            InitializeComponent();
            _importSvc = new ImportTxtService(new DocumentGenerator());
        }

        private void BtnUploadInfo_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var tup = svc.LoadImportSheetViaDialog("INFO_EXCEL");
            _importFileId = tup.Item1;
            _infoRows = tup.Item2;
            MessageBox.Show(_infoRows?.Count > 0
                ? $"INFO_EXCEL: {_infoRows.Count} rows (FileID={_importFileId})"
                : "No INFO_EXCEL loaded.");
        }

        private void BtnUploadInside_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var tup = svc.LoadImportSheetViaDialog("INSIDE_EXCEL");
            if (_importFileId == Guid.Empty)
                _importFileId = tup.Item1;
            _insideRows = tup.Item2;
            MessageBox.Show(_insideRows?.Count > 0
                ? $"INSIDE_EXCEL: {_insideRows.Count} rows"
                : "No INSIDE_EXCEL loaded.");
        }

        private async void BtnProcessImport_Click(object sender, RoutedEventArgs e)
        {
            if (_importFileId == Guid.Empty
             || ((_infoRows?.Count ?? 0) + (_insideRows?.Count ?? 0)) == 0)
            {
                MessageBox.Show("Please upload at least one sheet.");
                return;
            }

            // Disable UI
            BtnProcessImport.IsEnabled = false;
            TxtLog.Items.Clear();
            PbProgress.Value = 0;
            PbProgress.Visibility = Visibility.Visible;

            // Progress reporter that updates both bar & log
            var progress = new Progress<string>(msg => Log(msg));

            try
            {
                _txtPaths = await Task.Run(() =>
                    _importSvc.ProcessImport(
                        _importFileId,
                        _infoRows ?? new ObservableCollection<RowView>(),
                        _insideRows ?? new ObservableCollection<RowView>(),
                        progress
                    )
                );

                // enable download buttons
                BtnDownloadA.IsEnabled = _txtPaths.Count >= 1;
                BtnDownloadB.IsEnabled = _txtPaths.Count >= 2;
                BtnDownloadC.IsEnabled = _txtPaths.Count >= 3;
                BtnImportSap.IsEnabled = true;

                MessageBox.Show("All sheets generated successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import:\n{ex.Message}");
            }
            finally
            {
                PbProgress.Visibility = Visibility.Hidden;
                BtnProcessImport.IsEnabled = true;
            }
        }

        private void Download(string name, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var dlg = new SaveFileDialog
            {
                Title = $"Save {name}",
                Filter = "Text Files (*.txt)|*.txt",
                FileName = name
            };
            if (dlg.ShowDialog() == true)
                System.IO.File.Copy(path, dlg.FileName, overwrite: true);
        }

        private void BtnDownloadA_Click(object sender, RoutedEventArgs e)
            => Download("SheetA.txt", _txtPaths.ElementAtOrDefault(0));

        private void BtnDownloadB_Click(object sender, RoutedEventArgs e)
            => Download("SheetB.txt", _txtPaths.ElementAtOrDefault(1));

        private void BtnDownloadC_Click(object sender, RoutedEventArgs e)
            => Download("SheetC.txt", _txtPaths.ElementAtOrDefault(2));

        private void BtnImportSap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _importSvc.ImportIntoSap(_txtPaths);
                MessageBox.Show("Import into SAP completed.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SAP import failed:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Appends a line to the on‐screen log and scrolls it into view.
        /// </summary>
        private void Log(string msg)
        {
            TxtLog.Items.Add(msg);
            TxtLog.ScrollIntoView(msg);

            // If you also want to update the progress bar from messages like "PROGRESS:42"
            // you could parse them here and set PbProgress.Value accordingly.
            // e.g. if (msg.StartsWith("PROGRESS:")) { PbProgress.Value = double.Parse(msg.Substring(9)); }
        }
    }
}
