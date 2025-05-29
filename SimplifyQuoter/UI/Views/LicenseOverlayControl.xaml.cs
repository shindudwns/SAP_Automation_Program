// File: Views/LicenseOverlayControl.xaml.cs
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimplifyQuoter.Services;

namespace SimplifyQuoter.Views
{
    public partial class LicenseOverlayControl : UserControl
    {
        private const double SCALE = 0.7;
        private const string VERSION = "1.0";

        public LicenseOverlayControl()
        {
            InitializeComponent();
            TxtLicenseCode.TextChanged += (_, __) => UpdateContinue();
            ChkAgree.Checked += (_, __) => UpdateContinue();
            ChkAgree.Unchecked += (_, __) => UpdateContinue();
            BtnAgree.Click += async (_, __) => await OnAgreeAsync();
            Loaded += (s, e) =>
            {
                var win = Window.GetWindow(this);
                if (win != null)
                {
                    win.SizeChanged += (ws, we) => ResizeDialog(win);
                    ResizeDialog(win);
                }
            };
        }

        private void ResizeDialog(Window win)
        {
            DialogBorder.Width = win.ActualWidth * SCALE;
            DialogBorder.Height = win.ActualHeight * SCALE;
        }

        private void UpdateContinue()
        {
            BtnAgree.IsEnabled =
                !string.IsNullOrWhiteSpace(TxtLicenseCode.Text) &&
                ChkAgree.IsChecked == true;
        }

        private async Task OnAgreeAsync()
        {
            // Your existing validation & logging logic…
            this.Visibility = Visibility.Collapsed;
        }
    }
}
