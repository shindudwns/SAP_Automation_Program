using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class ProcessWindow : Window
    {
        private readonly List<RowView> _selectedRows;

        public ProcessWindow(List<RowView> selectedRows)
        {
            InitializeComponent();
            _selectedRows = selectedRows;

            // Prepare and bind the grid
            var list = new ObservableCollection<ProcessedRow>();
            foreach (var rv in _selectedRows)
            {
                var pr = new ProcessedRow
                {
                    RowIndex = rv.RowIndex,
                    CustomerName = rv.Cells.Length > 6 ? rv.Cells[6] : string.Empty,
                    ItemCode = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                    Quantity = decimal.TryParse(rv.Cells.Length > 3 ? rv.Cells[3] : null, out var q) ? q : 0,
                    RawDuration = rv.Cells.Length > 10 ? rv.Cells[10] : string.Empty
                };
                pr.FreeText = Transformer.ConvertDurationToFreeText(pr.RawDuration);
                list.Add(pr);
            }
            ProcessGrid.ItemsSource = list;
        }

        // Called when user clicks "Process Item Master Data"
        private void BtnItemMaster_Click(object sender, RoutedEventArgs e)
        {
            // TODO: iterate _selectedRows and call your Item Master Data automation
            MessageBox.Show("Item Master Data automation not yet implemented.");
        }

        // Called when user clicks "Process Sales Quotation"
        private void BtnSalesQuote_Click(object sender, RoutedEventArgs e)
        {
            // TODO: iterate _selectedRows and call your Sales Quotation automation
            MessageBox.Show("Sales Quotation automation not yet implemented.");
        }
    }

    // DTO for binding to ProcessGrid
    public class ProcessedRow
    {
        public int RowIndex { get; set; }
        public string CustomerName { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public string RawDuration { get; set; }
        public string FreeText { get; set; }
    }
}
