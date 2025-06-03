// File: MainWindow.xaml.cs
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
            MessageBox.Show(
                "This feature is under construction.",
                "Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}
