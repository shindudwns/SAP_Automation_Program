using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.ObjectModel;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Views;

namespace SimplifyQuoter
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<RowView> _rows;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var rows = svc.LoadSheetViaDialog();
            if (rows == null || rows.Count == 0) return;

            _rows = rows;
            BuildGridColumns();
            SheetGrid.ItemsSource = _rows;
        }

        private void BuildGridColumns()
        {
            SheetGrid.Columns.Clear();
            SheetGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Row",
                Binding = new Binding("RowIndex"),
                IsReadOnly = true,
                Width = DataGridLength.Auto
            });

            int cols = _rows[0].Cells.Length;
            for (int i = 0; i < cols; i++)
            {
                SheetGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"Col {i + 1}",
                    Binding = new Binding($"Cells[{i}]"),
                    IsReadOnly = true,
                    Width = DataGridLength.Auto
                });
            }
        }

        private void SheetGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            // clear all
            foreach (var rv in _rows) rv.IsSelected = false;
            // mark rows that have any selected cell
            foreach (var cell in SheetGrid.SelectedCells)
                if (cell.Item is RowView rv)
                    rv.IsSelected = true;
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            // collect only those rows
            var selectedRows = _rows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("Please select at least one row.");
                return;
            }

            // open the next window
            var win = new ProcessWindow(selectedRows);
            win.Owner = this;
            win.ShowDialog();
        }
    }
}
