using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class ProcessWindow : Window
    {
        // Public ctor matching how MainWindow calls it:
        public ProcessWindow(List<RowView> selectedRows)
        {
            InitializeComponent();

            // Map each RowView → ProcessedRow DTO
            var list = new ObservableCollection<ProcessedRow>();
            foreach (var rv in selectedRows)
            {
                var pr = new ProcessedRow
                {
                    RowIndex = rv.RowIndex,
                    CustomerName = rv.Cells.Length > 6 ? rv.Cells[6] : string.Empty,
                    ItemCode = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                    Quantity = decimal.TryParse(rv.Cells.Length > 3 ? rv.Cells[3] : null, out var q) ? q : 0,
                    FreeText = Transformer.ConvertDurationToFreeText(
                                      rv.Cells.Length > 10 ? rv.Cells[10] : string.Empty)
                };
                list.Add(pr);
            }

            // Bind the grid
            ProcessGrid.ItemsSource = list;
        }
    }

    // DTO class for display
    public class ProcessedRow
    {
        public int RowIndex { get; set; }
        public string CustomerName { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public string FreeText { get; set; }
    }
}
