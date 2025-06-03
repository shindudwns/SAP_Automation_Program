using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using SimplifyQuoter.Models;
using System.Windows.Navigation;      
using SimplifyQuoter.Views;         

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 2: SelectItemsPage lets the user pick rows (by range or by clicking).
    /// Updates WizardState.Current.SelectedRows and then navigates to ReviewConfirmPage.
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

            // Build columns for DataGridSelected
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

            // ====== ↓ COPY USER INPUTS INTO THE SHARED STATE ↓ ======
            // 1) Parse Margin % from txt box:
            if (double.TryParse(TxtMargin.Text.Trim(), out double marginVal))
            {
                state.MarginPercent = marginVal;
            }
            else
            {
                // If parsing fails, default back to 0 or previous:
                state.MarginPercent = 0.0;
            }

            // 2) Grab UoM text from the combo box:
            state.UoM = CmbUoM.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state.UoM))
            {
                // If the user didn’t select anything, default to “EACH”
                state.UoM = "EACH";
            }
            // =======================================================

            // Now navigate to ReviewConfirmPage.xaml:
            var nav = NavigationService.GetNavigationService(this);
            if (nav != null)
            {
                // If this UserControl is hosted in a Frame/NavigationWindow:
                nav.Navigate(new ReviewConfirmPage());
                return;
            }

            // Fallback: if NavigationService is null (e.g. you swap pages manually):
            var wizard = Window.GetWindow(this) as WizardWindow;
            if (wizard != null)
            {
                wizard.ShowStep(3);
                // ← Assumes that step index 3 is ReviewConfirmPage. 
                //     Adjust the step number according to your WizardWindow implementation.
            }

            // Optionally, you can still raise ProceedToReview if something else listens to it:
            ProceedToReview?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Public wrapper so that WizardWindow can “simulate” clicking Next.
        /// Returns true if navigation-to-review was allowed (i.e. at least one row was selected).
        /// </summary>
        public bool TryProceedToReview()
        {
            // Copy of BtnNext_Click’s validation & state‐copy logic, 
            // except we do NOT re‐invoke NavigationService here.
            var state = AutomationWizardState.Current;

            if (state.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one row (via range or by clicking).",
                                "Selection Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return false;
            }

            // 1) Parse Margin % from txt box:
            if (double.TryParse(TxtMargin.Text.Trim(), out double marginVal))
            {
                state.MarginPercent = marginVal;
            }
            else
            {
                state.MarginPercent = 0.0;
            }

            // 2) Grab UoM text from the combo box:
            state.UoM = CmbUoM.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state.UoM))
            {
                state.UoM = "EACH";
            }

            // Raise the same event that BtnNext_Click would have:
            ProceedToReview?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
