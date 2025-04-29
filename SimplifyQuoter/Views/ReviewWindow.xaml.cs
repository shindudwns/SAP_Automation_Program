using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.ObjectModel;
using SimplifyQuoter.Models;
using SimplifyQuoter.Views;    // for ProcessWindow

namespace SimplifyQuoter.Views
{
    public partial class ReviewWindow : Window
    {
        private readonly ObservableCollection<RowView> _rows;
        private readonly Guid _sapFileId;

        public ReviewWindow(Guid sapFileId, ObservableCollection<RowView> rows)
        {
            InitializeComponent();

            _sapFileId = sapFileId;
            _rows = rows;

            BuildGridColumns();
            ReviewGrid.ItemsSource = _rows;
        }

        private void BuildGridColumns()
        {
            ReviewGrid.Columns.Clear();

            // Row number
            ReviewGrid.Columns.Add(new DataGridTextColumn
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
                ReviewGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"Col {i + 1}",
                    Binding = new Binding($"Cells[{i}]"),
                    IsReadOnly = true,
                    Width = DataGridLength.Auto
                });
            }
        }

        private void ReviewGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            // mirror MainWindow logic to update RowView.IsSelected
            foreach (var rv in _rows)
                rv.IsSelected = false;

            foreach (var cell in ReviewGrid.SelectedCells)
                if (cell.Item is RowView rv)
                    rv.IsSelected = true;
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            var selected = _rows.Where(r => r.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Please drag‐select at least one row before processing.");
                return;
            }

            // now open your existing ProcessWindow
            var procWin = new ProcessWindow(_sapFileId, selected);
            procWin.Owner = this;
            procWin.ShowDialog();
        }
    }
}
