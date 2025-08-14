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
using System.Linq;
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
        private int _totalCount;
        private int _currentCount;
        private bool _skipAi;
        private bool _isLoading;
        public event EventHandler ProceedToProcess;
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


        public int CurrentCount
        {
            get => _currentCount;
            private set
            {
                if (_currentCount != value)
                {
                    _currentCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LoadingText));
                }
            }
        }
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LoadingText));
                }
            }
        }

        public string LoadingText => $"Loading… ({CurrentCount}/{TotalCount})";

        public ReviewConfirmPage()
        {
            InitializeComponent();

            DataContext = this;
            Loaded += ReviewConfirmPage_Loaded;

        }

        private async void BtnSkipAI_Click(object sender, RoutedEventArgs e)
        {
            //_skipAi = true;
            //BtnSkipAI.IsEnabled = false;
            _skipAi = true;
            AutomationWizardState.Current.SkipAI = true; // 전역에도 반영
            BtnSkipAI.IsEnabled = false;

            await RebuildAsync(); // 즉시 미리보기 다시 생성

        }


        /// <summary>
        /// On load (or whenever the control re‐appears), rebuild both DTO lists.
        /// 
        /// If a RowView has ≥11 cells (i.e. Sequence + 10 preview columns), use
        /// ToItemDtoFromEditedRow(...) to trust exactly columns 1–10. Otherwise,
        /// fall back to the AI/DB route via ToItemDtoAsync(...).
        /// </summary>
        /// 
        private async void ReviewConfirmPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 전역 상태 → 지역 플래그 동기화
            _skipAi = AutomationWizardState.Current.SkipAI;

            // 버튼 상태도 전역값 기준으로 맞춤
            if (BtnSkipAI != null)
                BtnSkipAI.IsEnabled = !_skipAi;

            // 공통 재빌드
            await RebuildAsync();
        }

        //private async void ReviewConfirmPage_Loaded(object sender, RoutedEventArgs e)
        //{
        //    var state = AutomationWizardState.Current;

        //    // BEFORE STARTING ANY async calls, show the Loading overlay:
        //    IsLoading = true;
        //    TotalCount = state.SelectedRows.Count;
        //    CurrentCount = 0;

        //    try
        //    {
        //        // 1) Build ItemMaster DTOs using Transformer.ToItemDtoAsync
        //        _currentItemMasterDtos = new List<ItemDto>();
        //        double marginPct = state.MarginPercent;
        //        string uom = state.UoM;

        //        foreach (var rv in state.SelectedRows)
        //        {
        //            // This call may run AI/DB or not AI lookups
        //            ItemDto dto = _skipAi
        //                ? Transformer.ToItemDtoWithoutAI(rv, marginPct, uom)
        //                : await Transformer.ToItemDtoAsync(rv, marginPct, uom);

        //            _currentItemMasterDtos.Add(dto);
        //            CurrentCount++;


        //        }

        //        _currentQuotationDtos = state.SelectedRows
        //            .Select(rv => Transformer.ToQuotationDto(rv))
        //            .ToList();

        //        TabItem_QuotePreview.Header =
        //            $"Quotation Preview ({_currentQuotationDtos.Count})";

        //        BuildItemMasterColumns();
        //        BuildQuotationColumns();
        //        BindItemMasterGrid();

        //        QuotationDataGrid.Visibility = Visibility.Collapsed;
        //    }
        //    catch (Exception ex)
        //    {
        //        // If something goes wrong during loading, you might want to show a message:
        //        MessageBox.Show(
        //            $"An error occurred while loading preview data:\n{ex.Message}",
        //            "Load Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error);
        //    }
        //    finally
        //    {
        //        // ───────────────────────────────────────────────────────────
        //        // AFTER ALL async calls have completed (or on error), hide Loading:
        //        IsLoading = false;

        //        // ───────────────────────────────────────────────────────────
        //    }
        //}
        private async Task RebuildAsync()
        {
            var state = AutomationWizardState.Current;

            // Loading 표시
            IsLoading = true;
            TotalCount = state.SelectedRows.Count;
            CurrentCount = 0;

            try
            {
                _currentItemMasterDtos = new List<ItemDto>();
                double marginPct = state.MarginPercent;
                string uom = state.UoM;

                foreach (var rv in state.SelectedRows)
                {
                    // 전역/지역 둘 중 하나라도 Skip이면 AI 미사용
                    var useSkip = _skipAi || state.SkipAI;

                    ItemDto dto = useSkip
                        ? Transformer.ToItemDtoWithoutAI(rv, marginPct, uom)
                        : await Transformer.ToItemDtoAsync(rv, marginPct, uom);

                    _currentItemMasterDtos.Add(dto);
                    CurrentCount++;
                }

                _currentQuotationDtos = state.SelectedRows
                    .Select(rv => Transformer.ToQuotationDto(rv))
                    .ToList();

                TabItem_QuotePreview.Header = $"Quotation Preview ({_currentQuotationDtos.Count})";

                BuildItemMasterColumns();
                BuildQuotationColumns();
                BindItemMasterGrid();

                QuotationDataGrid.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while loading preview data:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
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

                // 1) Read the edited preview sheet (it may be ItemMaster or Quotation format).
                List<RowView> previewRows = ExcelService.Instance.ReadReviewSheet(chosenPath);

                // 2) Grab the original selection (for sequence counts only).
                var state = AutomationWizardState.Current;
                var originalSelected = state.SelectedRows;

                // 3) Build a dictionary: sequenceNumber → edited RowView
                var seqToEdited = new Dictionary<int, RowView>();
                foreach (var rvNew in previewRows)
                {
                    if (!int.TryParse(rvNew.Cells.ElementAtOrDefault(0)?.Trim(), out int seq) || seq < 1)
                        continue;
                    seqToEdited[seq] = rvNew;
                }

                // 4) Merge old + new (or take new if beyond original count)
                var mergedRowViews = new List<RowView>();
                foreach (var kvp in seqToEdited.OrderBy(k => k.Key))
                {
                    var rvNew = kvp.Value;
                    if (kvp.Key <= originalSelected.Count)
                    {
                        var rvOld = originalSelected[kvp.Key - 1];
                        int colCount = Math.Max(rvOld.Cells.Length, rvNew.Cells.Length);
                        var merged = new string[colCount];

                        for (int c = 0; c < colCount; c++)
                        {
                            var oldText = c < rvOld.Cells.Length ? rvOld.Cells[c] : string.Empty;
                            var newText = c < rvNew.Cells.Length ? rvNew.Cells[c] : string.Empty;
                            merged[c] = string.IsNullOrWhiteSpace(newText)
                                ? oldText
                                : (!string.Equals(oldText?.Trim(), newText?.Trim(), StringComparison.Ordinal)
                                    ? newText.Trim()
                                    : oldText);
                        }

                        mergedRowViews.Add(new RowView
                        {
                            RowIndex = rvOld.RowIndex,
                            RowId = rvOld.RowId,
                            Cells = merged
                        });
                    }
                    else
                    {
                        mergedRowViews.Add(new RowView
                        {
                            RowIndex = rvNew.RowIndex,
                            RowId = Guid.Empty,
                            Cells = rvNew.Cells
                        });
                    }
                }

                // 5) Re-build DTO lists from mergedRowViews
                _currentItemMasterDtos = new List<ItemDto>(mergedRowViews.Count);
                _currentQuotationDtos = new List<QuotationDto>(mergedRowViews.Count);
                string chosenUoM = state.UoM;

                foreach (var rv in mergedRowViews)
                {
                    // 5.a) ItemMaster DTO
                    _currentItemMasterDtos.Add(
                        Transformer.ToItemDtoFromEditedRow(rv, chosenUoM)
                    );

                    // 5.b) Quotation DTO
                    double qty = 0;
                    double.TryParse(
                        rv.Cells.ElementAtOrDefault(4)?.Replace(",", ""),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out qty
                    );

                    var freeText = rv.Cells.ElementAtOrDefault(5) ?? string.Empty;

                    _currentQuotationDtos.Add(new QuotationDto
                    {
                        CardCode = rv.Cells.ElementAtOrDefault(1) ?? string.Empty,
                        DocDate = DateTime.Today,
                        DocumentLines = new List<QuotationLineDto> {
                    new QuotationLineDto {
                        ItemCode = rv.Cells.ElementAtOrDefault(3) ?? string.Empty,
                        Quantity = qty,
                        FreeText = freeText
                    }
                }
                    });
                }

                // 6) Update wizard state with the merged DTOs
                state.MergedItemMasterDtos = new List<ItemDto>(_currentItemMasterDtos);
                state.MergedQuotationDtos = new List<QuotationDto>(_currentQuotationDtos);

                // 7) Refresh the preview grids
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
                    MessageBoxImage.Error
                );
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
            public string Mainsupplier => _dto.Mainsupplier ?? string.Empty;
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
