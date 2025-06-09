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
        // Show copy cursor when dragging a file over the drop zone
        private void DropBorder_DragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        // Same as DragEnter—keeps cursor if you hover
        private void DropBorder_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        // Your existing Drop handler
        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;

            var path = files[0];
            if (!Path.GetExtension(path)
                     .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Only .xlsx files are supported.", "Drop Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadFile(path);
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
            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Sample Excel File",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "sample.xlsx"
            };
            if (saveDlg.ShowDialog() != true) return;

            try
            {
                // ← Replace 'SimplifyQuoter' with your actual assembly/namespace if different
                var resourceUri = new Uri(
                    "pack://application:,,,/SimplifyQuoter;component/sample.xlsx",
                    UriKind.Absolute);

                var resInfo = Application.GetResourceStream(resourceUri);
                if (resInfo == null)
                    throw new Exception("Sample file not found as a Resource.");

                using (var resourceStream = resInfo.Stream)
                using (var fileStream = File.Create(saveDlg.FileName))
                {
                    resourceStream.CopyTo(fileStream);
                }

                MessageBox.Show("Sample file saved to:\n" + saveDlg.FileName,
                                "Download Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save sample file:\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

        }


    }
}
