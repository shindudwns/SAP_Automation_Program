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

                //var user = (Application.Current.Properties["CurrentUser"] as string ?? string.Empty).Trim();
                //if (string.Equals(user, "Young", StringComparison.OrdinalIgnoreCase))
                //    BtnLog.Visibility = System.Windows.Visibility.Visible;
                //else
                //    BtnLog.Visibility = System.Windows.Visibility.Collapsed;
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
            //    //try
            //    //{
            //    //    // Import TXT 전용 창을 모달로 띄움
            //    //    var win = new SimplifyQuoter.Views.ImportWindow
            //    //    {
            //    //        Owner = this
            //    //    };
            //    //    win.ShowDialog();
            //    //}
            //    //catch (Exception ex)
            //    //{
            //    //    MessageBox.Show(
            //    //        $"Failed to open Import TXT window:\n{ex.Message}",
            //    //        "Error",
            //    //        MessageBoxButton.OK,
            //    //        MessageBoxImage.Error
            //    //    );
            //    //}
            //    MessageBox.Show(
            //        //"This feature is under construction.",
            //        "Hello, World!",
            //        "Coming Soon",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Information
            //    );
            //    //}
            //    var state = AutomationWizardState.Current;

            //    // SAP Automation 버튼과 동일한 로그인 가드
            //    if (state.SlClient == null || !state.SlClient.IsLoggedIn)
            //    {
            //        MessageBox.Show(
            //            "You must be logged in to use SAP Automation.",
            //            "Not Logged In",
            //            MessageBoxButton.OK,
            //            MessageBoxImage.Warning
            //        );
            //        return;
            //    }

            //    // 마법사 창을 띄우되, 열기 전에 바로 Step 3으로 이동
            //    var wizard = new SimplifyQuoter.Views.WizardWindow
            //    {
            //        Owner = this
            //    };
            //    wizard.ShowStep(3);   // ★ 핵심 한 줄
            //    wizard.ShowDialog();
            //}
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
        }
}
