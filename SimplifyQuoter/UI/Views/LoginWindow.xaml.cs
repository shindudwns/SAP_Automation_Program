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

        // The XAML must define these named controls:
        //   <TextBox x:Name="txtCompanyDb" …/>
        //   <TextBox x:Name="txtUserName" …/>
        //   <PasswordBox x:Name="txtPassword" …/>
        //   <TextBox x:Name="txtLicenseCode" …/>

        public string CompanyDB => txtCompanyDb.Text.Trim();
        public string UserName => txtUserName.Text.Trim();
        public string Password => txtPassword.Password;
        public string LicenseCode => txtLicenseCode.Text.Trim();

        public LoginWindow()
        {
            InitializeComponent();
            SlClient = new ServiceLayerClient();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // 1) First, check license code in the database:
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

            // 2) License is valid → proceed with Service Layer login
            try
            {
                await SlClient.LoginAsync(CompanyDB, UserName, Password);

                // 3) On successful login, write acceptance_log row
                using (var db = new DatabaseService())
                {
                    // Retrieve local IP address (or you can hard-code 127.0.0.1 if preferred)
                    string localIp = GetLocalIPAddress();

                    // Get machine name as a proxy for device info
                    string deviceInfo = Environment.MachineName;

                    // Example version – you can change or pull this from config
                    string agreementVersion = "1.0";

                    db.LogAcceptance(
                        licenseCode: LicenseCode,
                        licenseAccept: true,
                        agreementVersion: agreementVersion,
                        deviceInfo: deviceInfo,
                        ipAddress: localIp
                    );
                }

                // 4) Close dialog with success
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

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;  // Closes dialog and ShowDialog() returns false
        }

        /// <summary>
        /// Get the first non‐loopback IPv4 address of this machine.
        /// </summary>
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
