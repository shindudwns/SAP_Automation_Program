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
            // When user closes it, execution returns here.
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // 1) Check that LicenseCode is non‐empty
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

            // 2) Validate the license code in the database
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
                await SlClient.LoginAsync(CompanyDB, UserName, Password);

                // 4) On successful login, write to acceptance_log
                using (var db = new DatabaseService())
                {
                    string localIp = GetLocalIPAddress();
                    string deviceInfo = Environment.MachineName;
                    string agreementVersion = "1.0"; // Or pull from config

                    db.LogAcceptance(
                        licenseCode: LicenseCode,
                        licenseAccept: true,
                        agreementVersion: agreementVersion,
                        deviceInfo: deviceInfo,
                        ipAddress: localIp
                    );
                }

                // 5) Close the login dialog with success
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Service Layer login failed:\n{ex.Message}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                // Do NOT set DialogResult, so ShowDialog() returns false
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
