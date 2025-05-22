// File: Views/ProcessWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;

namespace SimplifyQuoter.Views
{
    public partial class ProcessWindow : Window
    {
        private readonly Guid _sapFileId;
        private readonly List<RowView> _rows;

        private readonly ServiceLayerClient _slClient;
        private readonly AutomationService _autoSvc;

        // Cached credentials
        private string _companyDb;
        private string _userName;
        private string _password;

        public ProcessWindow(Guid sapFileId, List<RowView> rows)
        {
            InitializeComponent();
            _sapFileId = sapFileId;
            _rows = rows;

            // === Service Layer wiring ===
            _slClient = new ServiceLayerClient();
            var itemSvc = new ItemService(_slClient);
            var quoteSvc = new QuotationService(_slClient);

            _autoSvc = new AutomationService(_sapFileId, itemSvc, quoteSvc);

            // === Populate Item Master Data grid ===
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

            // === Populate Sales Quotation grid ===
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

        // Legacy overload (if needed)
        public ProcessWindow(List<RowView> rows)
            : this(Guid.Empty, rows)
        {
        }

        private async void BtnProcessIMD_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!await EnsureLoggedInAsync())
                    return;

                await _autoSvc.RunItemMasterDataAsync(_rows);
                await _slClient.LogoutAsync();

                MessageBox.Show("Item Master Data processed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"IMD error: {ex.Message}");
            }
        }

        private async void BtnProcessSQ_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!await EnsureLoggedInAsync())
                    return;

                await _autoSvc.RunSalesQuotationAsync(_rows);
                await _slClient.LogoutAsync();

                MessageBox.Show("Sales Quotations processed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SQ error: {ex.Message}");
            }
        }

        /// <summary>
        /// Prompt for credentials if not already logged in.
        /// </summary>
        private async Task<bool> EnsureLoggedInAsync()
        {
            if (_slClient.IsLoggedIn)
                return true;

            var loginWin = new LoginWindow { Owner = this };
            if (loginWin.ShowDialog() != true)
                return false;

            _companyDb = loginWin.CompanyDB;
            _userName = loginWin.UserName;
            _password = loginWin.Password;

            await _slClient.LoginAsync(_companyDb, _userName, _password);
            return true;
        }

        /// <summary>
        /// No-op handler for SQ grid selection (wired in XAML).
        /// </summary>
        private void SqGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        // ----- DTOs for DataGrid binding -----
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
