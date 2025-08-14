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
            // (optional debugging)
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
