using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using Microsoft.Win32;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class ImportWindow : Window
    {
        private ObservableCollection<RowView> _infoRows;
        private ObservableCollection<RowView> _insideRows;
        private IList<DataTable> _sheets;
        private IList<string> _txtPaths;

        private readonly ImportTxtService _importSvc;

        public ImportWindow()
        {
            InitializeComponent();
            _importSvc = new ImportTxtService(
                new DocumentGenerator(),
                new AutomationService());
        }

        private void BtnUploadInfo_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            _infoRows = svc.LoadSheetViaDialog();
            MessageBox.Show(_infoRows?.Count > 0
                ? $"INFO_EXCEL: {_infoRows.Count} rows loaded"
                : "No INFO_EXCEL loaded.");
        }

        private void BtnUploadInside_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            _insideRows = svc.LoadSheetViaDialog();
            MessageBox.Show(_insideRows?.Count > 0
                ? $"INSIDE_EXCEL: {_insideRows.Count} rows loaded"
                : "No INSIDE_EXCEL loaded.");
        }

        private void BtnProcessImport_Click(object sender, RoutedEventArgs e)
        {
            if ((_infoRows?.Count ?? 0) + (_insideRows?.Count ?? 0) == 0)
            {
                MessageBox.Show("Please upload at least one sheet.");
                return;
            }

            // 1) build DataTables
            _sheets = _importSvc.GenerateImportSheets(_infoRows, _insideRows);

            // 2) export to temp .txt
            _txtPaths = _importSvc.ExportToTxt(_sheets);

            // enable downloads & SAP import
            BtnDownloadA.IsEnabled = true;
            BtnDownloadB.IsEnabled = true;
            BtnDownloadC.IsEnabled = true;
            BtnImportSap.IsEnabled = true;

            MessageBox.Show("Three sheets generated and ready for download or import.");
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

        private void BtnDownloadA_Click(object sender, RoutedEventArgs e)
            => Download("SheetA.txt", _txtPaths[0]);

        private void BtnDownloadB_Click(object sender, RoutedEventArgs e)
            => Download("SheetB.txt", _txtPaths[1]);

        private void BtnDownloadC_Click(object sender, RoutedEventArgs e)
            => Download("SheetC.txt", _txtPaths[2]);

        private void BtnImportSap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _importSvc.ImportIntoSap(_txtPaths);
                MessageBox.Show("Import into SAP completed.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SAP import failed: {ex.Message}");
            }
        }
    }
}
