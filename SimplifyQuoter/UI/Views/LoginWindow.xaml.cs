// File: Views/LoginWindow.xaml.cs
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;

namespace SimplifyQuoter.Views
{
    public partial class LoginWindow : Window
    {
        public ServiceLayerClient SlClient { get; private set; }

        public string CompanyDB => txtCompanyDb.Text.Trim();
        public string UserName => txtUserName.Text.Trim();
        public string Password => txtPassword.Password;
        public string LicenseCode => txtLicenseCode.Text.Trim();

        public LoginWindow()
        {
            InitializeComponent();
            SlClient = new ServiceLayerClient();
        }

        private void BtnViewLicense_Click(object sender, RoutedEventArgs e)
        {
            // Create and show LicenseWindow modally
            var licenseWindow = new LicenseWindow
            {
                Owner = this
            };
            licenseWindow.ShowDialog();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // 1) Ensure LicenseCode is entered
            if (string.IsNullOrWhiteSpace(LicenseCode))
            {
                MessageBox.Show(
                    "Please enter a license code.",
                    "License Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 2) Check license in database
            bool isValidCode;
            using (var db = new DatabaseService())
            {
                isValidCode = db.IsLicenseCodeValid(LicenseCode);
            }

            if (!isValidCode)
            {
                MessageBox.Show(
                    "The license code you entered is invalid or expired.",
                    "Invalid License",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // 3) Attempt Service Layer login
            try
            {
                // CompanyDB and Password and UserName come from your LoginWindow’s fields
                await SlClient.LoginAsync(CompanyDB, UserName, Password);

                // 4) Log acceptance (now passing UserName as user_id)
                using (var db = new DatabaseService())
                {
                    string localIp = GetLocalIPAddress();
                    string deviceInfo = Environment.MachineName;
                    string agreementVersion = "1.0"; // Or read from config

                    db.LogAcceptance(
                        userId: UserName,             
                        licenseCode: LicenseCode,
                        licenseAccept: true,
                        agreementVersion: agreementVersion,
                        deviceInfo: deviceInfo,
                        ipAddress: localIp
                    );
                }

                // 5) Indicate success, close dialog
                DialogResult = true;   // <— This makes ShowDialog() return “true”
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Service Layer login failed:\n{ex.Message}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                // Don’t set DialogResult, so ShowDialog() returns false
            }
        }


        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host
                    .AddressList
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                return ip?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
