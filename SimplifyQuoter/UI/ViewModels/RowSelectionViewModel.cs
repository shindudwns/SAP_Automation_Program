using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Utilities;

namespace SimplifyQuoter.ViewModels
{
    public class RowSelectionViewModel : ViewModelBase
    {
        private readonly ExcelService _excelSvc = new ExcelService();

        private ObservableCollection<RowView> _rows;
        public ObservableCollection<RowView> Rows
        {
            get { return _rows; }
            set { _rows = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DataGridCellInfo> _selectedCells
            = new ObservableCollection<DataGridCellInfo>();
        public ObservableCollection<DataGridCellInfo> SelectedCells
        {
            get { return _selectedCells; }
            set { _selectedCells = value; OnPropertyChanged(); }
        }

        private ICommand _uploadCommand;
        public ICommand UploadCommand
        {
            get
            {
                if (_uploadCommand == null)
                    _uploadCommand = new RelayCommand(param => Upload());
                return _uploadCommand;
            }
        }

        private void Upload()
        {
            var loaded = _excelSvc.LoadSheetViaDialog();
            if (loaded != null)
                Rows = loaded;
        }

        public void CaptureSelection(DataGrid grid)
        {
            var cells = grid.SelectedCells.Cast<DataGridCellInfo>();
            SelectedCells = new ObservableCollection<DataGridCellInfo>(cells);
        }
    }
}
