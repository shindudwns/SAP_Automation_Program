// Models/RowView.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimplifyQuoter.Models
{
    public class RowView : INotifyPropertyChanged
    {
        public int RowIndex { get; set; }
        public string[] Cells { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
