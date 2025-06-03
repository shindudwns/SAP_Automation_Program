// File: Views/UploadPage.xaml.cs
using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 1: UploadPage lets the user drag-drop or browse for an Excel (.xlsx).
    /// Once loaded, it raises FileLoaded so the wizard can advance to Step 2.
    /// </summary>
    public partial class UploadPage : UserControl
    {
        public event EventHandler FileLoaded;

        public UploadPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// “Browse files” button click → open a file dialog
        /// </summary>
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenAndLoad();
        }

        /// <summary>
        /// Handles a drag‐and‐drop onto the dashed rectangle
        /// </summary>
        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Path.GetExtension(files[0])
                                         .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    LoadFile(files[0]);
                }
            }
        }

        /// <summary>
        /// Show an OpenFileDialog to pick a .xlsx file
        /// </summary>
        private void OpenAndLoad()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select SMK_EXCEL for SAP Automation"
            };
            if (dlg.ShowDialog() != true)
                return;

            LoadFile(dlg.FileName);
        }

        /// <summary>
        /// Core logic: given a path, read with ExcelService, store into shared state, fire FileLoaded
        /// </summary>
        private void LoadFile(string path)
        {
            try
            {
                var svc = new ExcelService();
                var result = svc.LoadSapSheet(path);
                var fileId = result.Item1;
                var rows = result.Item2;

                if (rows == null || rows.Count == 0)
                {
                    MessageBox.Show("No rows found in that file.", "Upload Error",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var state = AutomationWizardState.Current;
                state.SapFileId = fileId;
                state.AllRows = rows;

                // Fire the event so WizardWindow can advance to Step 2
                FileLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Excel file:\n{ex.Message}",
                                "Upload Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// “Download sample file” hyperlink click → open a URL in the default browser
        /// </summary>
        private void DownloadSample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://example.com/path/to/sample.xlsx",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Unable to open sample file link.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
