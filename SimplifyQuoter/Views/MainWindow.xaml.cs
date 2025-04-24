using System;
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
        private Guid _sapFileId;

        public MainWindow()
        {
            InitializeComponent();
            // DB connection test here…
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var svc = new ExcelService();
            var result = svc.LoadSapSheetViaDialog();
            _sapFileId = result.Item1;
            _rows = result.Item2;

            if (_rows == null || !_rows.Any())
                return;

            BuildGridColumns();
            SheetGrid.ItemsSource = _rows;
        }

        private void BuildGridColumns()
        {
            SheetGrid.Columns.Clear();

            // Row number column
            SheetGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Row",
                Binding = new Binding("RowIndex"),
                IsReadOnly = true,
                Width = DataGridLength.Auto
            });

            // One column per Excel cell
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
            if (_rows == null) return;
            foreach (var rv in _rows)
                rv.IsSelected = false;
            foreach (var cell in SheetGrid.SelectedCells)
                if (cell.Item is RowView rv)
                    rv.IsSelected = true;
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            var selected = _rows?.Where(r => r.IsSelected).ToList();
            if (selected == null || !selected.Any())
            {
                MessageBox.Show("Please select at least one row.");
                return;
            }

            var win = new ProcessWindow(_sapFileId, selected);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var win = new ImportWindow();
            win.Owner = this;
            win.ShowDialog();
        }
    }
}
