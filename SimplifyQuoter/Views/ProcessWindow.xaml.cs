using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using System.Windows.Controls;

namespace SimplifyQuoter.Views
{
    public partial class ProcessWindow : Window
    {
        private readonly Guid _sapFileId;
        private readonly List<RowView> _rows;
        private readonly AutomationService _autoSvc = new AutomationService();

        // new DB-backed constructor
        public ProcessWindow(Guid sapFileId, List<RowView> rows)
        {
            InitializeComponent();
            _sapFileId = sapFileId;
            _rows = rows;

            // IMD grid
            var imdList = new ObservableCollection<ImdRow>(
                _rows.Select((rv, idx) => new ImdRow
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

            // SQ grid
            var sqList = new ObservableCollection<SqRow>(
                _rows.Select((rv, idx) => new SqRow
                {
                    Sequence = idx + 1,
                    Name = rv.Cells.Length > 6 ? rv.Cells[6] : string.Empty,
                    ItemNo = rv.Cells.Length > 2 ? rv.Cells[2] : string.Empty,
                    Quantity = rv.Cells.Length > 3 ? rv.Cells[3] : string.Empty,
                    FreeText = Transformer.ConvertDurationToFreeText(
                                 rv.Cells.Length > 10 ? rv.Cells[10] : string.Empty)
                })
            );
            SqGrid.ItemsSource = sqList;
        }

        // legacy overload if you still need it:
        public ProcessWindow(List<RowView> rows)
          : this(Guid.Empty, rows) { }

        private void BtnProcessIMD_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoSvc.RunItemMasterData(_sapFileId, _rows);
                MessageBox.Show("IMD done");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"IMD error: {ex.Message}");
            }
        }

        private void BtnProcessSQ_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoSvc.RunSalesQuotation(_sapFileId, _rows);
                MessageBox.Show("SQ done");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SQ error: {ex.Message}");
            }
        }

        /// <summary>
        /// Dummy handler so the XAML SelectionChanged="SqGrid_SelectionChanged" compiles.
        /// You can later wire this up if you need to respond to SQ‐grid selection.
        /// </summary>
        private void SqGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // no‐op for now
        }

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

        public class SqRow
        {
            public int Sequence { get; set; }
            public string Name { get; set; }
            public string ItemNo { get; set; }
            public string Quantity { get; set; }
            public string FreeText { get; set; }
        }
    }
}
