// File: MainWindow.xaml.cs
using System;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Services;
using SimplifyQuoter.Views;

namespace SimplifyQuoter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var allowed = new System.Collections.Generic.HashSet<string>(
                    new[] { "Young" }, StringComparer.OrdinalIgnoreCase); // 이름 추가시 추가 로그 사용자 추가 가능
            try
            {
                var user = (Application.Current.Properties["CurrentUser"] as string ?? string.Empty).Trim();
                // [CHANGED-ADD-ONLY] 기존 if 대신 아래 한 줄만 사용해도 됨(원본 if는 남겨두고 주석)
                // if (string.Equals(user, "Young", StringComparison.OrdinalIgnoreCase))
                if (allowed.Contains(user))
                    BtnLog.Visibility = Visibility.Visible;
                else
                    BtnLog.Visibility = Visibility.Collapsed;


            }
            catch
            {
                BtnLog.Visibility = System.Windows.Visibility.Collapsed;

            }
        }
        // [NEW] Log 버튼 클릭 → 로그 전용 창 오픈
        private void BtnLog_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var w = new SimplifyQuoter.Views.LogWindow
            {
                Owner = this
            };
            w.Show();
        }

        private void BtnSapAutomation_Click(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;
            if (state.SlClient == null || !state.SlClient.IsLoggedIn)
            {
                MessageBox.Show(
                    "You must be logged in to use SAP Automation.",
                    "Not Logged In",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Launch the wizard directly—no further login prompt
            var wizard = new WizardWindow
            {
                Owner = this
            };
            wizard.ShowDialog();
        }



        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var state = AutomationWizardState.Current;
            if (state.SlClient == null || !state.SlClient.IsLoggedIn)
            {
                MessageBox.Show(
                    "You must be logged in to use SAP Automation.",
                    "Not Logged In",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var wizard = new SimplifyQuoter.Views.WizardWindow(startStep: 3) { Owner = this };
            wizard.ShowDialog();
        }
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                // Audit 헬퍼를 이미 만들었다면 이 라인만 사용
                SimplifyQuoter.Services.Audit.Log("logout");

                // [대안 - Audit.cs가 없다면 아래 두 줄로 대체]
                // var user = (Application.Current.Properties["CurrentUser"] as string ?? "").Trim();
                // using (var db = new DatabaseService()) db.LogEvent(user, "logout", null, Environment.MachineName, null);
            }
            catch { /* no-op */ }
        }
    }

}
