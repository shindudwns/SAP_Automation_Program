// File: Views/LoginWindow.xaml.cs
using System.Windows;
using SimplifyQuoter.Services.ServiceLayer;

namespace SimplifyQuoter.Views
{
    public partial class LoginWindow : Window
    {
        public ServiceLayerClient SlClient { get; private set; }

        public string CompanyDB => txtCompanyDb.Text.Trim();
        public string UserName => txtUserName.Text.Trim();
        public string Password => txtPassword.Password;

        public LoginWindow()
        {
            InitializeComponent();
            SlClient = new ServiceLayerClient();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SlClient.LoginAsync(CompanyDB, UserName, Password);
                // If we get here, login succeeded:
                DialogResult = true;    // <— This makes ShowDialog() return true
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Service Layer login failed:\n{ex.Message}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                // Leave DialogResult unset so ShowDialog() yields false/nullable
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;      // <— This makes ShowDialog() return false
        }
    }
}
