// File: Views/LoginWindow.xaml.cs
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Properties; // Settings.Default 쓰려고

namespace SimplifyQuoter.Views
{
    public partial class LoginWindow : Window
    {
        public ServiceLayerClient SlClient { get; private set; }

        // Bound to the TextBoxes / PasswordBox in XAML:
        public string CompanyDB => txtCompanyDb.Text.Trim();
        public string UserName => txtUserName.Text.Trim();
        public string Password => txtPassword.Password;
        public string LicenseCode => txtLicenseCode.Text.Trim();

        public LoginWindow()
        {
            InitializeComponent();
            SlClient = new ServiceLayerClient();
            //if (string.IsNullOrWhiteSpace(txtLicenseCode.Text))
            //    txtLicenseCode.Text = "aa";
            // 저장된 설정 불러오기
            chkRememberId.IsChecked = Properties.Settings.Default.RememberId;
            if (Properties.Settings.Default.RememberId)
                txtUserName.Text = Properties.Settings.Default.SavedUserId ?? string.Empty;
        }

        private void BtnViewLicense_Click(object sender, RoutedEventArgs e)
        {
            // Show the LicenseWindow modally:
            var licenseWindow = new LicenseWindow
            {
                Owner = this
            };
            licenseWindow.ShowDialog();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            //// 1) Ensure LicenseCode is entered
            //if (string.IsNullOrWhiteSpace(LicenseCode))
            //{
            //    MessageBox.Show(
            //        "Please enter a license code.",
            //        "License Required",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Warning
            //    );
            //    return;
            //}

            // 2) Check license in database
            //bool isValidCode;
            //using (var db = new DatabaseService())
            //{
            //    isValidCode = db.IsLicenseCodeValid(LicenseCode);  라이센스 요구 코드삭제
            //}

            //if (!isValidCode)
            //{
            //    MessageBox.Show(
            //        "The license code you entered is invalid or expired.",
            //        "Invalid License",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Error
            //    );
            //    return;
            //}

            // 3) Attempt Service Layer login
            try
            {
                await SlClient.LoginAsync(CompanyDB, UserName, Password);

                // ✅ 로그인 성공했으니 Remember ID 반영
                var remember = chkRememberId.IsChecked == true;
                Properties.Settings.Default.RememberId = remember;
                Properties.Settings.Default.SavedUserId = remember ? UserName : string.Empty;
                Properties.Settings.Default.Save();

                // 4) Store the logged‐in userID into the shared state:
                AutomationWizardState.Current.UserName = UserName;
                // Also store the SL client so that downstream pages can reuse it:
                AutomationWizardState.Current.SlClient = SlClient;

                // 5) Log acceptance (now passing UserName as user_id)
                using (var db = new DatabaseService())
                {
                    string localIp = GetLocalIPAddress();
                    string deviceInfo = Environment.MachineName;
                    string agreementVersion = "1.0"; // or read from config

                    db.LogAcceptance(
                        userId: UserName,
                        licenseCode: LicenseCode,
                        licenseAccept: true,
                        agreementVersion: agreementVersion,
                        deviceInfo: deviceInfo,
                        ipAddress: localIp
                    );
                }
                Application.Current.Properties["CurrentUser"] = txtUserName.Text?.Trim();

                var mw = new SimplifyQuoter.MainWindow();
                mw.Show();
                Application.Current.MainWindow = mw; // 안전용(선택): 로그인 창이 MainWindow였던 경우 대비

                // 6) Indicate success, close dialog
                //DialogResult = true;  // This will make ShowDialog() return “true”
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Password Incorrected.\n{ex.Message}",
                    "Password Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // 재입력 편의
                txtPassword.Clear();
                txtPassword.Focus();
                // Do not set DialogResult, so ShowDialog() returns false
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

        private void txtLicenseCode_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
