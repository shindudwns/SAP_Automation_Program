using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;  

namespace SimplifyQuoter.Views
{
    public partial class ProcessWindow : Window
    {
        public ProcessWindow(List<RowView> selectedRows)
        {
            InitializeComponent();

            int total = selectedRows.Count;

            // Build and bind IMD list (unchanged)…
            var imdList = new ObservableCollection<ImdRow>(
                selectedRows.Select((rv, idx) => new ImdRow
                {
                    Sequence = idx + 1,
                    ItemNo = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                    Description = string.Empty,
                    PartNumber = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                    ItemGroup = string.Empty,
                    Manufacturer = rv.Cells.Length > 5 ? rv.Cells[5] : string.Empty,
                    PreferredVendor = string.Empty,
                    PurchasingUoMName = "EACH",
                    SalesUoMName = "EACH",
                    UoMName = "EACH"
                })
            );
            ImdGrid.ItemsSource = imdList;

            // Build and bind SQ list, now with Korean→“WEEK ERO” logic
            var sqList = new ObservableCollection<SqRow>(
                selectedRows.Select((rv, idx) => {
                    // raw value from column K (0-based index 10)
                    string raw = rv.Cells.Length > 10 ? rv.Cells[10] : string.Empty;
                    return new SqRow
                    {
                        Sequence = idx + 1,
                        Name = rv.Cells.Length > 6 ? rv.Cells[6] : string.Empty,
                        ItemNo = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                        Quantity = rv.Cells.Length > 3 ? rv.Cells[3] : string.Empty,
                        FreeText = Transformer.ConvertDurationToFreeText(raw)
                    };
                })
            );
            SqGrid.ItemsSource = sqList;
        }

        private void BtnProcessIMD_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Will be implemented");
        }

        private void BtnProcessSQ_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Will be implemented");
        }

        // DTO for Item Master Data tab
        public class ImdRow
        {
            public int Sequence { get; set; }
            public string ItemNo { get; set; }
            public string Description { get; set; }
            public string PartNumber { get; set; }
            public string ItemGroup { get; set; }
            public string Manufacturer { get; set; }
            public string PreferredVendor { get; set; }
            public string PurchasingUoMName { get; set; }
            public string SalesUoMName { get; set; }
            public string UoMName { get; set; }
        }

        // DTO for Sales Quotation tab
        public class SqRow
        {
            public int Sequence { get; set; }
            public string Name { get; set; }
            public string ItemNo { get; set; }
            public string Quantity { get; set; }
            public string FreeText { get; set; }
        }

        private void SqGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
