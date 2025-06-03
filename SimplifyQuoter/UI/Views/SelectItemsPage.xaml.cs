// File: Views/SelectItemsPage.xaml.cs
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 2: SelectItemsPage lets the user pick rows (by range or by clicking).
    /// Updates WizardState.Current.SelectedRows and raises ProceedToReview.
    /// </summary>
    public partial class SelectItemsPage : UserControl
    {
        public event EventHandler ProceedToReview;

        public SelectItemsPage()
        {
            InitializeComponent();
            Loaded += SelectItemsPage_Loaded;
        }

        private void SelectItemsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;

            if (state.AllRows == null || state.AllRows.Count == 0)
                return;

            // Build columns for DataGridAllRows
            BuildGridColumns(DataGridAllRows, state.AllRows);

            DataGridAllRows.ItemsSource = state.AllRows;

            // Build columns for DataGridSelected (just RowIndex + PartNumber)
            DataGridSelected.Columns.Clear();
            DataGridSelected.Columns.Add(new DataGridTextColumn
            {
                Header = "Row",
                Binding = new System.Windows.Data.Binding("RowIndex"),
                Width = DataGridLength.Auto
            });
            DataGridSelected.Columns.Add(new DataGridTextColumn
            {
                Header = "Part Number",
                Binding = new System.Windows.Data.Binding("Cells[2]"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            DataGridSelected.ItemsSource = state.SelectedRows;
        }

        private void BuildGridColumns(DataGrid grid, System.Collections.ObjectModel.ObservableCollection<RowView> rows)
        {
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Row",
                Binding = new System.Windows.Data.Binding("RowIndex"),
                IsReadOnly = true,
                Width = DataGridLength.Auto
            });

            int cols = rows[0].Cells.Length;
            for (int i = 0; i < cols; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"Col {i + 1}",
                    Binding = new System.Windows.Data.Binding($"Cells[{i}]"),
                    IsReadOnly = true,
                    Width = DataGridLength.Auto
                });
            }
        }

        private void DataGridAllRows_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            var state = AutomationWizardState.Current;
            state.SelectedRows.Clear();

            foreach (var cell in DataGridAllRows.SelectedCells)
            {
                if (cell.Item is RowView rv && !state.SelectedRows.Contains(rv))
                    state.SelectedRows.Add(rv);
            }

            DataGridSelected.Items.Refresh();
        }

        private void BtnApplyRange_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtRange.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            var state = AutomationWizardState.Current;
            state.SelectedRows.Clear();

            // Parse segments "1-5", "8", etc.
            var segments = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var seg in segments)
            {
                var part = seg.Trim();
                if (part.Contains('-'))
                {
                    var pieces = part.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (pieces.Length == 2
                        && int.TryParse(pieces[0], out int start)
                        && int.TryParse(pieces[1], out int end))
                    {
                        for (int i = start; i <= end; i++)
                        {
                            var rv = state.AllRows.FirstOrDefault(r => r.RowIndex == i);
                            if (rv != null && !state.SelectedRows.Contains(rv))
                                state.SelectedRows.Add(rv);
                        }
                    }
                }
                else if (int.TryParse(part, out int single))
                {
                    var rv = state.AllRows.FirstOrDefault(r => r.RowIndex == single);
                    if (rv != null && !state.SelectedRows.Contains(rv))
                        state.SelectedRows.Add(rv);
                }
            }

            DataGridSelected.Items.Refresh();

            // Temporarily unsubscribe from SelectedCellsChanged so UnselectAll() doesn't clear our selection
            DataGridAllRows.SelectedCellsChanged -= DataGridAllRows_SelectedCellsChanged;
            DataGridAllRows.UnselectAll();

            // Copy the selected rows into a separate list before iterating
            var toSelect = state.SelectedRows.ToList();
            foreach (var rv in toSelect)
            {
                DataGridAllRows.SelectedItems.Add(rv);
            }

            // Re-subscribe to the event after we finish
            DataGridAllRows.SelectedCellsChanged += DataGridAllRows_SelectedCellsChanged;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            (this.Parent as WizardWindow)?.ShowStep(1);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;
            if (state.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one row (via range or by clicking).",
                                "Selection Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }
            ProceedToReview?.Invoke(this, EventArgs.Empty);
        }
    }
}
