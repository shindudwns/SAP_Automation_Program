using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            var result = svc.LoadImportSheetViaDialog("INFO_EXCEL");
            _importFileId = result.Item1;
            _infoRows = result.Item2;

            MessageBox.Show(
                _infoRows != null && _infoRows.Count > 0
                  ? $"INFO_EXCEL: {_infoRows.Count} rows loaded (File ID: {_importFileId})"
                  : "No INFO_EXCEL loaded."
            );
        }

        private void BtnUploadInside_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            // reuse same importFileId
            var result = svc.LoadImportSheetViaDialog("INSIDE_EXCEL");
            _insideRows = result.Item2;
            // if Info hasn’t set it, use this file’s ID
            if (_importFileId == Guid.Empty)
            _importFileId = result.Item1;

            MessageBox.Show(
                _insideRows != null && _insideRows.Count > 0
                  ? $"INSIDE_EXCEL: {_insideRows.Count} rows loaded"
                  : "No INSIDE_EXCEL loaded."
            );
        }

        private void BtnProcessImport_Click(object sender, RoutedEventArgs e)
        {
            if (_importFileId == Guid.Empty ||
               ((_infoRows?.Count ?? 0) + (_insideRows?.Count ?? 0) == 0))
            {
                MessageBox.Show("Please upload at least one sheet.");
                return;
            }

            try
            {
                var path = _importSvc.ProcessImport(
                    _importFileId,
                    _infoRows ?? new ObservableCollection<RowView>(),
                    _insideRows ?? new ObservableCollection<RowView>());

                _txtPaths = new List<string> { path };
                BtnDownloadA.IsEnabled = true;
                BtnDownloadB.IsEnabled = false;  // only Sheet A for now
                BtnDownloadC.IsEnabled = false;
                BtnImportSap.IsEnabled = true;

                MessageBox.Show($"SheetA.txt generated:\n{path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during processing:\n{ex.Message}");
            }
        }

        private void BtnDownloadA_Click(object sender, RoutedEventArgs e)
        {
            SaveFile("SheetA.txt", _txtPaths[0]);
        }

        private void BtnDownloadB_Click(object sender, RoutedEventArgs e)
        {
            if (_txtPaths.Count > 1)
                SaveFile("SheetB.txt", _txtPaths[1]);
        }

        private void BtnDownloadC_Click(object sender, RoutedEventArgs e)
        {
            if (_txtPaths.Count > 2)
                SaveFile("SheetC.txt", _txtPaths[2]);
        }

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

        private void SaveFile(string defaultName, string srcPath)
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
    }
}
