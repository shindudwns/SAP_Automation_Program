// File: MainWindow.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Views;

namespace SimplifyQuoter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Debug.WriteLine("[MainWindow] InitializeComponent succeeded.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Exception in MainWindow.InitializeComponent():\n" +
                    $"{ex.GetType().Name}: {ex.Message}",
                    "MainWindow Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                throw;
            }

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("MainWindow Loaded successfully!", "Debug", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void BtnSapAutomation_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var result = svc.LoadSapSheetViaDialog();
            var sapFileId = result.Item1;
            var rows = result.Item2;

            if (rows == null || !rows.Any())
                return;

            var review = new ReviewWindow(
                sapFileId,
                new ObservableCollection<RowView>(rows)
            );
            review.Owner = this;
            review.ShowDialog();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "This feature is under construction.",
                "Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}
