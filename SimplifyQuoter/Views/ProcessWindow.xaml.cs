// File: Views/ProcessWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

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

            _slClient = new ServiceLayerClient();
            var itemSvc = new ItemService(_slClient);
            var quoteSvc = new QuotationService(_slClient);
            _autoSvc = new AutomationService(_sapFileId, itemSvc, quoteSvc);

            // Defer grid‐population until after window is loaded
            Loaded += ProcessWindow_Loaded;
        }

        private async void ProcessWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) For each RowView, await the AI‐enriched DTO
            var dtos = await Task.WhenAll(
                _rows.Select(rv => Transformer.ToItemDtoAsync(rv)));

            // 2) Build ViewModels with the real ItemDto
            var imdList = new ObservableCollection<ImdRowViewModel>(
                dtos.Select((dto, idx) =>
                    new ImdRowViewModel(_rows[idx].RowId, idx + 1, dto))
            );

            ImdGrid.ItemsSource = imdList;

            // Sales‐quote grid stays synchronous
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
                //var resp = await _slClient.HttpClient.GetAsync("$metadata");
                //resp.EnsureSuccessStatusCode();
                //var xml = await resp.Content.ReadAsStringAsync();
                //Console.WriteLine(xml);

                // <<< Insert this diagnostic check here >>>
    //            var resp = await _slClient.HttpClient
    //.GetAsync("BusinessPartners('VL000442')?$select=CardCode,CardName,CardType");
    //            resp.EnsureSuccessStatusCode();
    //            var json = await resp.Content.ReadAsStringAsync();
    //            Debug.WriteLine("BP VL000442 details:\n" + json);
    //            or pop it into a MessageBox so you can eyeball it:

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

            // Show your custom login dialog
            var loginWin = new LoginWindow { Owner = this };
            if (loginWin.ShowDialog() != true)
                return false;

            // Pull the values the user entered
            _companyDb = loginWin.CompanyDB;
            _userName = loginWin.UserName;
            _password = loginWin.Password;

            // THIS is where you call LoginAsync—wrap it in try/catch:
            try
            {
                await _slClient.LoginAsync(_companyDb, _userName, _password);
            }
            catch (Exception ex)
            {
                // Show the full error (including SL’s JSON body) in a MessageBox
                MessageBox.Show(
                    $"Service Layer login failed:\n{ex.Message}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }

            return true;
        }


        /// <summary>No-op handler for SQ grid selection (wired in XAML).</summary>
        private void SqGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        // ----- ViewModel for Item Master Data grid -----
        public class ImdRowViewModel
        {
            public Guid RowId { get; }
            public int Sequence { get; }
            public ItemDto Dto { get; }

            public ImdRowViewModel(Guid rowId, int seq, ItemDto dto)
            {
                RowId = rowId;
                Sequence = seq;
                Dto = dto;
            }


            public string ItemNo => Dto.ItemCode;
            public string Description => Dto.ItemName;
            public string PartNumber => Dto.FrgnName;

            // TODO: Check if ItemGroup is INT or STRING
            public int ItemGroup => Dto.ItmsGrpCod;
            public string PreferredVendor => Dto.CardCode ?? string.Empty;
            public string PurchasingUoM => Dto.PurchasingUoM ?? string.Empty;
            public string SalesUoM => Dto.SalesUoM ?? string.Empty;
            public string InventoryUoM => Dto.InventoryUoM ?? string.Empty;
        }

        // ----- DTOs for SQ grid binding (unchanged) -----
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
