using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            MessageBox.Show(
              _infoRows?.Count > 0
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
            MessageBox.Show(
              _insideRows?.Count > 0
                ? $"INSIDE_EXCEL: {_insideRows.Count} rows"
                : "No INSIDE_EXCEL loaded.");
        }

        private async void BtnProcessImport_Click(object sender, RoutedEventArgs e)
        {
            if (_importFileId == Guid.Empty
             || ((_infoRows?.Count ?? 0) + (_insideRows?.Count ?? 0) == 0))
            {
                MessageBox.Show("Please upload at least one sheet.");
                return;
            }

            BtnProcessImport.IsEnabled = false;
            try
            {
                // Run the *sync* ProcessImport on a background thread
                string path = await Task.Run(() =>
                    _importSvc.ProcessImport(
                        _importFileId,
                        _infoRows ?? new ObservableCollection<RowView>(),
                        _insideRows ?? new ObservableCollection<RowView>()
                    )
                );

                _txtPaths = new List<string> { path };
                BtnDownloadA.IsEnabled = true;
                BtnDownloadB.IsEnabled = false;
                BtnDownloadC.IsEnabled = false;
                BtnImportSap.IsEnabled = true;
                MessageBox.Show($"SheetA.txt generated:\n{path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import:\n{ex.Message}");
            }
            finally
            {
                BtnProcessImport.IsEnabled = true;
            }
        }

        private void Download(string defaultName, string srcPath)
        {
            var dlg = new SaveFileDialog
            {
                Title = $"Save {defaultName}",
                Filter = "Text Files (*.txt)|*.txt",
                FileName = defaultName
            };
            if (dlg.ShowDialog() == true)
                System.IO.File.Copy(srcPath, dlg.FileName, overwrite: true);
        }

        private void BtnDownloadA_Click(object s, RoutedEventArgs e)
            => Download("SheetA.txt", _txtPaths[0]);
        private void BtnDownloadB_Click(object s, RoutedEventArgs e)
            => Download("SheetB.txt", _txtPaths.Count > 1 ? _txtPaths[1] : null);
        private void BtnDownloadC_Click(object s, RoutedEventArgs e)
            => Download("SheetC.txt", _txtPaths.Count > 2 ? _txtPaths[2] : null);

        private void BtnImportSap_Click(object s, RoutedEventArgs e)
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
    }
}
