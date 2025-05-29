using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Views
{
    public partial class ReviewWindow : Window
    {
        private readonly ObservableCollection<RowView> _rows;
        private readonly Guid _sapFileId;

        private List<(int rowIndex, int colIndex)> _searchHits = new List<(int, int)>();
        private int _searchIndex = -1;

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

            ReviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Row",
                Binding = new Binding("RowIndex"),
                IsReadOnly = true,
                Width = DataGridLength.Auto
            });

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

            var procWin = new ProcessWindow(_sapFileId, selected);
            procWin.Owner = this;
            procWin.ShowDialog();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SearchBox.Visibility = Visibility.Visible;
                SearchBox.Focus();
                e.Handled = true;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_searchHits.Count == 0)
                {
                    SearchAndMove(SearchBox.Text.Trim());
                }
                else
                {
                    _searchIndex = (_searchIndex + 1) % _searchHits.Count;
                    MoveToSearchHit();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SearchBox.Visibility = Visibility.Collapsed;
                _searchHits.Clear();
                _searchIndex = -1;
                e.Handled = true;
            }
        }

        private void SearchAndMove(string keyword)
        {
            _searchHits.Clear();
            _searchIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                for (int j = 0; j < _rows[i].Cells.Length; j++)
                {
                    if (!string.IsNullOrEmpty(_rows[i].Cells[j]) &&
                        _rows[i].Cells[j].IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _searchHits.Add((i, j));
                    }
                }
            }

            if (_searchHits.Count > 0)
            {
                _searchIndex = 0;
                MoveToSearchHit();
            }
            else
            {
                MessageBox.Show("No matches found.");
            }
        }

        private void MoveToSearchHit()
        {
            if (_searchIndex < 0 || _searchIndex >= _searchHits.Count)
                return;

            var (row, col) = _searchHits[_searchIndex];
            ReviewGrid.SelectedCells.Clear();
            ReviewGrid.ScrollIntoView(_rows[row]);

            var colDef = ReviewGrid.Columns[col + 1]; // offset by 1 due to "RowIndex" column
            var cellInfo = new DataGridCellInfo(_rows[row], colDef);
            ReviewGrid.SelectedCells.Add(cellInfo);
        }
    }
}
