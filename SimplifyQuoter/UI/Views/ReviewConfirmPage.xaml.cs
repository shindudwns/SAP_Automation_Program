// File: Views/ReviewConfirmPage.xaml.cs
using Microsoft.Win32;
using OfficeOpenXml;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer.Dtos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SimplifyQuoter.Views
{
    /// <summary>
    /// Step 3: ReviewConfirmPage shows two previews (ItemMaster vs. Quotation).
    /// Allows Download/Replace Excel, then Confirm & Process.
    /// </summary>
    public partial class ReviewConfirmPage : UserControl, INotifyPropertyChanged
    {
        // Stored lists of DTOs for each tab:
        private List<ItemDto> _currentItemMasterDtos;
        private List<QuotationDto> _currentQuotationDtos;

        /// <summary>
        /// When true, the “Loading…” overlay appears.
        /// </summary>
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler ProceedToProcess;

        public ReviewConfirmPage()
        {
            InitializeComponent();

            // Set the DataContext so that our bindings (e.g. IsLoading) work:
            DataContext = this;

            Loaded += ReviewConfirmPage_Loaded;
        }

        /// <summary>
        /// On load (or whenever the control re‐appears), rebuild both DTO lists.
        /// 
        /// If a RowView has ≥11 cells (i.e. Sequence + 10 preview columns), use
        /// ToItemDtoFromEditedRow(...) to trust exactly columns 1–10. Otherwise,
        /// fall back to the AI/DB route via ToItemDtoAsync(...).
        /// </summary>
        private async void ReviewConfirmPage_Loaded(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;

            // ─────────────────────────────────────────────────────────────
            // BEFORE STARTING ANY async calls, show the Loading overlay:
            IsLoading = true;
            // ─────────────────────────────────────────────────────────────

            try
            {
                // 1) Build ItemMaster DTOs using Transformer.ToItemDtoAsync
                double marginPct = state.MarginPercent;
                string uom = state.UoM;

                _currentItemMasterDtos = new List<ItemDto>();
                foreach (var rv in state.SelectedRows)
                {
                    // This call may run AI/DB lookups, so it can take time:
                    var dto = await Transformer.ToItemDtoAsync(rv, marginPct, uom);
                    _currentItemMasterDtos.Add(dto);
                }

                // 2) Build Quotation DTOs using Transformer.ToQuotationDto
                _currentQuotationDtos = new List<QuotationDto>();
                foreach (var rv in state.SelectedRows)
                {
                    var qdto = Transformer.ToQuotationDto(rv);
                    _currentQuotationDtos.Add(qdto);
                }

                // 3) Update the “(N)” on the Quotation tab header:
                TabItem_QuotePreview.Header = $"Quotation Preview ({_currentQuotationDtos.Count})";

                // 4) Build columns for each DataGrid
                BuildItemMasterColumns();
                BuildQuotationColumns();

                // 5) Bind the default (ItemMaster) DataGrid
                BindItemMasterGrid();

                // 6) Ensure QuotationDataGrid is hidden initially
                QuotationDataGrid.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // If something goes wrong during loading, you might want to show a message:
                MessageBox.Show(
                    $"An error occurred while loading preview data:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // ───────────────────────────────────────────────────────────
                // AFTER ALL async calls have completed (or on error), hide Loading:
                IsLoading = false;
                // ───────────────────────────────────────────────────────────
            }
        }


        #region ► BUILD & BIND DATA GRIDS

        private void BuildItemMasterColumns()
        {
            if (ItemMasterDataGrid == null) return;
            ItemMasterDataGrid.Columns.Clear();

            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "#",
                Binding = new System.Windows.Data.Binding("Sequence"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Item No.",
                Binding = new System.Windows.Data.Binding("ItemNo"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Description",
                Binding = new System.Windows.Data.Binding("Description"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Part Number",
                Binding = new System.Windows.Data.Binding("PartNumber"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Item Group",
                Binding = new System.Windows.Data.Binding("ItemGroup"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Preferred Vendor",
                Binding = new System.Windows.Data.Binding("PreferredVendor"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Purchasing UoM",
                Binding = new System.Windows.Data.Binding("PurchaseUnit"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Sales UoM",
                Binding = new System.Windows.Data.Binding("SalesUnit"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Inventory UOM",
                Binding = new System.Windows.Data.Binding("InventoryUOM"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Purchasing Price",
                Binding = new System.Windows.Data.Binding("U_PurchasingPrice"),
                Width = DataGridLength.Auto
            });
            ItemMasterDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Sales Price",
                Binding = new System.Windows.Data.Binding("U_SalesPrice"),
                Width = DataGridLength.Auto
            });
        }

        private void BuildQuotationColumns()
        {
            if (QuotationDataGrid == null) return;
            QuotationDataGrid.Columns.Clear();

            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "#",
                Binding = new System.Windows.Data.Binding("Sequence"),
                Width = DataGridLength.Auto
            });
            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Card Code",
                Binding = new System.Windows.Data.Binding("CardCode"),
                Width = DataGridLength.Auto
            });
            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Doc Date",
                Binding = new System.Windows.Data.Binding("DocDateFormatted"),
                Width = DataGridLength.Auto
            });
            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Item Code",
                Binding = new System.Windows.Data.Binding("Line.ItemCode"),
                Width = DataGridLength.Auto
            });
            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Quantity",
                Binding = new System.Windows.Data.Binding("Line.Quantity"),
                Width = DataGridLength.Auto
            });
            QuotationDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Free Text",
                Binding = new System.Windows.Data.Binding("Line.FreeText"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
        }

        private void BindItemMasterGrid()
        {
            if (ItemMasterDataGrid == null || _currentItemMasterDtos == null) return;

            var vmList = new ObservableCollection<ImdRowViewModel>();
            for (int i = 0; i < _currentItemMasterDtos.Count; i++)
            {
                vmList.Add(new ImdRowViewModel(i + 1, _currentItemMasterDtos[i]));
            }
            ItemMasterDataGrid.ItemsSource = vmList;
        }

        private void BindQuotationGrid()
        {
            if (QuotationDataGrid == null || _currentQuotationDtos == null) return;

            var vmList = new ObservableCollection<QuotationRowViewModel>();
            for (int i = 0; i < _currentQuotationDtos.Count; i++)
            {
                vmList.Add(new QuotationRowViewModel(i + 1, _currentQuotationDtos[i]));
            }
            QuotationDataGrid.ItemsSource = vmList;
        }

        #endregion


        #region ► TAB SELECTION CHANGED

        private void PreviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against early firing before controls are initialized:
            if (ItemMasterDataGrid == null || QuotationDataGrid == null)
                return;

            if (PreviewTabs.SelectedIndex == 0)
            {
                ItemMasterDataGrid.Visibility = Visibility.Visible;
                QuotationDataGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                ItemMasterDataGrid.Visibility = Visibility.Collapsed;
                QuotationDataGrid.Visibility = Visibility.Visible;
                BindQuotationGrid();
            }
        }

        #endregion


        #region ► TOOLBAR BUTTON HANDLERS

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Search feature not implemented yet.",
                "Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Save Preview as Excel"
                };

                if (PreviewTabs.SelectedIndex == 0)
                    dlg.FileName = "ItemMasterPreview.xlsx";
                else
                    dlg.FileName = "QuotationPreview.xlsx";

                if (dlg.ShowDialog() != true)
                    return;

                string outPath = dlg.FileName;

                if (PreviewTabs.SelectedIndex == 0)
                {
                    await ExcelService.Instance
                                  .WriteItemMasterPreviewAsync(_currentItemMasterDtos, outPath);
                }
                else
                {
                    await ExcelService.Instance
                                  .WriteQuotationPreviewAsync(_currentQuotationDtos, outPath);
                }

                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export Excel:\n{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnReplaceExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Select Edited Preview Excel"
                };
                if (dlg.ShowDialog() != true)
                    return;

                string chosenPath = dlg.FileName;

                List<RowView> previewRows = ExcelService.Instance.ReadReviewSheet(chosenPath);
                var state = AutomationWizardState.Current;
                var originalSelected = state.SelectedRows;

                if (previewRows.Count != originalSelected.Count)
                {
                    MessageBox.Show(
                        "The edited file’s row count does not match the original selection count.\n" +
                        "Make sure you did not add or remove rows in the Excel preview.",
                        "Row Count Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var mergedRowViews = new List<RowView>(previewRows.Count);

                foreach (var rvNew in previewRows)
                {
                    if (!int.TryParse(rvNew.Cells[0]?.Trim(), out int seq)
                        || seq < 1
                        || seq > originalSelected.Count)
                    {
                        MessageBox.Show(
                            $"Invalid sequence value '{rvNew.Cells[0]}' encountered in the preview sheet.\n" +
                            "Each row’s first column must be 1,2,3,… matching the original selection order.",
                            "Sequence Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    int originalIndex = seq - 1;
                    var rvOld = originalSelected[originalIndex];

                    int colCount = Math.Max(rvOld.Cells.Length, rvNew.Cells.Length);
                    var mergedCells = new string[colCount];

                    for (int i = 0; i < colCount; i++)
                    {
                        string oldText = (i < rvOld.Cells.Length) ? rvOld.Cells[i] : string.Empty;
                        string newText = (i < rvNew.Cells.Length) ? rvNew.Cells[i] : string.Empty;

                        if (string.IsNullOrWhiteSpace(newText))
                        {
                            mergedCells[i] = oldText;
                        }
                        else if (!string.Equals(oldText?.Trim(), newText?.Trim(), StringComparison.Ordinal))
                        {
                            mergedCells[i] = newText.Trim();
                        }
                        else
                        {
                            mergedCells[i] = oldText;
                        }
                    }

                    var rvMerged = new RowView
                    {
                        RowIndex = rvOld.RowIndex,
                        RowId = rvOld.RowId,
                        Cells = mergedCells
                    };
                    mergedRowViews.Add(rvMerged);
                }

                state.AllRows = new ObservableCollection<RowView>(mergedRowViews);
                state.SelectedRows.Clear();
                foreach (var rv in mergedRowViews)
                    state.SelectedRows.Add(rv);

                _currentItemMasterDtos = new List<ItemDto>(state.SelectedRows.Count);
                _currentQuotationDtos = new List<QuotationDto>(state.SelectedRows.Count);
                string chosenUoM = state.UoM;

                foreach (var rv in state.SelectedRows)
                {
                    var dto = Transformer.ToItemDtoFromEditedRow(rv, chosenUoM);
                    _currentItemMasterDtos.Add(dto);

                    string cardCode = (rv.Cells.Length > 1) ? rv.Cells[1]?.Trim() : string.Empty;

                    DateTime docDate = DateTime.Today;
                    if (rv.Cells.Length > 2 &&
                        DateTime.TryParseExact(rv.Cells[2]?.Trim(), "yyyy-MM-dd",
                                               CultureInfo.InvariantCulture,
                                               DateTimeStyles.None,
                                               out var parsedDate))
                    {
                        docDate = parsedDate;
                    }

                    string qItemCode = (rv.Cells.Length > 3) ? rv.Cells[3]?.Trim() : string.Empty;

                    double quantity = 0;
                    if (rv.Cells.Length > 4 && !string.IsNullOrWhiteSpace(rv.Cells[4]))
                    {
                        var rawQty = rv.Cells[4].Trim().Replace(",", "");
                        double.TryParse(rawQty,
                                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                                        CultureInfo.InvariantCulture,
                                        out quantity);
                    }

                    string freeText = (rv.Cells.Length > 5) ? rv.Cells[5]?.Trim() : string.Empty;

                    var qdto = new QuotationDto
                    {
                        CardCode = cardCode,
                        DocDate = docDate,
                        DocumentLines = new List<QuotationLineDto>
                        {
                            new QuotationLineDto
                            {
                                ItemCode = qItemCode,
                                Quantity = quantity,
                                FreeText = freeText
                            }
                        }
                    };
                    _currentQuotationDtos.Add(qdto);
                }

                // ─── STORE MERGED DTOS IN WIZARD STATE ─────────────────────────────────
                state.MergedItemMasterDtos = new List<ItemDto>(_currentItemMasterDtos);
                state.MergedQuotationDtos = new List<QuotationDto>(_currentQuotationDtos);
                // ─────────────────────────────────────────────────────────────────────

                if (PreviewTabs.SelectedIndex == 0)
                {
                    BuildItemMasterColumns();
                    BindItemMasterGrid();
                }
                else
                {
                    BuildQuotationColumns();
                    BindQuotationGrid();
                }

                TabItem_QuotePreview.Header = $"Quotation Preview ({_currentQuotationDtos.Count})";
                MessageBox.Show("Preview replaced successfully.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to replace preview:\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion


        #region ► Back & Confirm

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            (this.Parent as WizardWindow)?.ShowStep(2);
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ProceedToProcess?.Invoke(this, EventArgs.Empty);
        }

        #endregion


        #region ► ROW VIEWMODELS

        /// <summary>
        /// ViewModel for each row in ItemMasterDataGrid.
        /// </summary>
        public class ImdRowViewModel
        {
            public int Sequence { get; }
            private readonly ItemDto _dto;

            public ImdRowViewModel(int seq, ItemDto dto)
            {
                Sequence = seq;
                _dto = dto;
            }

            public string ItemNo => _dto.ItemCode;
            public string Description => _dto.ItemName;
            public string PartNumber => _dto.FrgnName;
            public int ItemGroup => _dto.ItmsGrpCod;
            public string PreferredVendor => _dto.BPCode ?? string.Empty;
            public string PurchaseUnit => _dto.PurchaseUnit ?? string.Empty;
            public string SalesUnit => _dto.SalesUnit ?? string.Empty;
            public string InventoryUOM => _dto.InventoryUOM ?? string.Empty;
            public double U_PurchasingPrice => _dto.U_PurchasingPrice;
            public double U_SalesPrice => _dto.U_SalesPrice;
        }

        /// <summary>
        /// ViewModel for each row in QuotationDataGrid.
        /// </summary>
        public class QuotationRowViewModel
        {
            public int Sequence { get; }
            public string CardCode { get; }
            public string DocDateFormatted { get; }
            public QuotationLineDto Line { get; }

            public QuotationRowViewModel(int seq, QuotationDto qdto)
            {
                Sequence = seq;
                CardCode = qdto.CardCode;
                DocDateFormatted = qdto.DocDate.ToString("yyyy-MM-dd");
                Line = (qdto.DocumentLines.Count > 0)
                                  ? qdto.DocumentLines[0]
                                  : new QuotationLineDto();
            }
        }

        #endregion


        // ────────── INotifyPropertyChanged ──────────
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
