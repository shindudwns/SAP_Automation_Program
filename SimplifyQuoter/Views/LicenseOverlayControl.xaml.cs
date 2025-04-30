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
        private const double SCALE = 0.8;
        private const string VERSION = "1.0";

        public LicenseOverlayControl()
        {
            InitializeComponent();

            // only enable the Continue button once code + agree are set
            TxtLicenseCode.TextChanged += (_, __) => UpdateContinue();
            ChkAgree.Checked += (_, __) => UpdateContinue();
            ChkAgree.Unchecked += (_, __) => UpdateContinue();

            BtnAgree.Click += async (_, __) => await OnAgreeAsync();

            // size the dialog to 80% of the hosting window
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
            string code = TxtLicenseCode.Text.Trim();

            // 1) Validate code from DB
            string company;
            DateTime? expires;
            bool valid;
            using (var svc = new LicenseService())
            {
                valid = svc.TryGetLicense(code, out company, out expires);
            }
            if (!valid)
            {
                MessageBox.Show(
                    "Unknown, inactive, or expired license code.",
                    "Invalid Code",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 2) Gather context
            string ip = await GetPublicIpAsync();
            string device = GetDeviceInfo();
            bool accepted = ChkAgree.IsChecked == true;
            DateTime when = DateTime.UtcNow;

            // 3) Log acceptance
            try
            {
                using (var svc = new LicenseService())
                {
                    svc.LogAcceptance(
                        ipAddress: ip,
                        timestamp: when,
                        version: VERSION,
                        deviceInfo: device,
                        licenseCode: code,
                        licenseAccept: accepted,
                        companyName: company
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to record acceptance:\n" + ex.Message,
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // 4) Dismiss the overlay
            this.Visibility = Visibility.Collapsed;
        }

        private async Task<string> GetPublicIpAsync()
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                    return await http.GetStringAsync("https://api.ipify.org");
            }
            catch
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    var lan = host.AddressList
                                   .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    return lan?.ToString() ?? "Unknown";
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        private string GetDeviceInfo()
        {
            var name = Environment.MachineName;
            var os = Environment.OSVersion.ToString();
            var runtime = Environment.Version.ToString();
            return $"{name} | {os} | CLR {runtime}";
        }
    }
}
