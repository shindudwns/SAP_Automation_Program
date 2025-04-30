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

        private void BtnUploadInfo_Click(object s, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var tup = svc.LoadImportSheetViaDialog("INFO_EXCEL");
            _importFileId = tup.Item1;
            _infoRows = tup.Item2;
            MessageBox.Show(_infoRows?.Count > 0
                ? $"INFO_EXCEL: {_infoRows.Count} rows (FileID={_importFileId})"
                : "No INFO_EXCEL loaded.");
        }

        private void BtnUploadInside_Click(object s, RoutedEventArgs e)
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

            BtnProcessImport.IsEnabled = false;
            TxtLog.Items.Clear();
            PbProgress.Value = 0;
            PbProgress.Visibility = Visibility.Visible;
            ProgressContainer.Visibility = Visibility.Visible;
            ProgressFill.Visibility = Visibility.Visible;

            // logger:
            var logProg = new Progress<string>(msg =>
            {
                TxtLog.Items.Add(msg);
                TxtLog.ScrollIntoView(msg);
            });

            // bar: update both the hidden ProgressBar (for Minimum/Maximum) and the Rectangle
            var barProg = new Progress<double>(pct =>
            {
                // clamp
                pct = Math.Max(0, Math.Min(100, pct));
                PbProgress.Value = pct;

                // directly drive the Rectangle’s Width:
                ProgressFill.Width = ProgressContainer.ActualWidth * (pct / 100.0);
            });

            try
            {
                _txtPaths = await Task.Run(() =>
                    _importSvc.ProcessImport(
                        _importFileId,
                        _infoRows ?? new ObservableCollection<RowView>(),
                        _insideRows ?? new ObservableCollection<RowView>(),
                        logProg,
                        barProg
                    )
                );

                BtnDownloadA.IsEnabled = _txtPaths.Count >= 1;
                BtnDownloadB.IsEnabled = _txtPaths.Count >= 2;
                BtnDownloadC.IsEnabled = _txtPaths.Count >= 3;
                BtnImportSap.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import:\n{ex.Message}");
            }
            finally
            {
                BtnProcessImport.IsEnabled = true;
                // hide the progress visuals if you like
                // PbProgress.Visibility      = Visibility.Collapsed;
                // ProgressFill.Visibility    = Visibility.Collapsed;
                // ProgressContainer.Visibility = Visibility.Collapsed;
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

        private void BtnDownloadA_Click(object s, RoutedEventArgs e)
            => Download("SheetA.txt", _txtPaths.ElementAtOrDefault(0));
        private void BtnDownloadB_Click(object s, RoutedEventArgs e)
            => Download("SheetB.txt", _txtPaths.ElementAtOrDefault(1));
        private void BtnDownloadC_Click(object s, RoutedEventArgs e)
            => Download("SheetC.txt", _txtPaths.ElementAtOrDefault(2));

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
