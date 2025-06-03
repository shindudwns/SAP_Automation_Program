// File: Views/ReviewConfirmPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer.Dtos;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 3: ReviewConfirmPage shows a read-only grid of all ItemDto payloads.
    /// On Confirm, raises ProceedToProcess so the wizard goes to Step 4.
    /// </summary>
    public partial class ReviewConfirmPage : UserControl
    {
        public event EventHandler ProceedToProcess;

        public ReviewConfirmPage()
        {
            InitializeComponent();
            Loaded += ReviewConfirmPage_Loaded;
        }

        private async void ReviewConfirmPage_Loaded(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;

            var dtos = new ItemDto[state.SelectedRows.Count];
            for (int i = 0; i < state.SelectedRows.Count; i++)
            {
                dtos[i] = await Transformer.ToItemDtoAsync(state.SelectedRows[i]);
            }

            ReviewImdGrid.Columns.Clear();
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "#",
                Binding = new System.Windows.Data.Binding("Sequence"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Item No.",
                Binding = new System.Windows.Data.Binding("ItemNo"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Description",
                Binding = new System.Windows.Data.Binding("Description"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Part Number",
                Binding = new System.Windows.Data.Binding("PartNumber"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Item Group",
                Binding = new System.Windows.Data.Binding("ItemGroup"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Preferred Vendor",
                Binding = new System.Windows.Data.Binding("PreferredVendor"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Purchasing UoM",
                Binding = new System.Windows.Data.Binding("PurchaseUnit"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Sales UoM",
                Binding = new System.Windows.Data.Binding("SalesUnit"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Inventory UOM",
                Binding = new System.Windows.Data.Binding("InventoryUOM"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Purchasing Price",
                Binding = new System.Windows.Data.Binding("U_PurchasingPrice"),
                Width = DataGridLength.Auto
            });
            ReviewImdGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Sales Price",
                Binding = new System.Windows.Data.Binding("U_SalesPrice"),
                Width = DataGridLength.Auto
            });

            var viewModels = new ObservableCollection<ImdRowViewModel>();
            for (int i = 0; i < dtos.Length; i++)
            {
                viewModels.Add(new ImdRowViewModel(
                    state.SelectedRows[i].RowId,
                    i + 1,
                    dtos[i]
                ));
            }

            ReviewImdGrid.ItemsSource = viewModels;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            (this.Parent as WizardWindow)?.ShowStep(2);
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ProceedToProcess?.Invoke(this, EventArgs.Empty);
        }

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
            public int ItemGroup => Dto.ItmsGrpCod;
            public string PreferredVendor => Dto.BPCode ?? string.Empty;
            public string PurchaseUnit => Dto.PurchaseUnit ?? string.Empty;
            public string SalesUnit => Dto.SalesUnit ?? string.Empty;
            public string InventoryUOM => Dto.InventoryUOM ?? string.Empty;
            public double U_PurchasingPrice => Dto.U_PurchasingPrice;
            public double U_SalesPrice => Dto.U_SalesPrice;
        }
    }
}
