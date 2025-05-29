// File: Views/LoginWindow.xaml.cs
using System.Windows;

namespace SimplifyQuoter.Views
{
    public partial class LoginWindow : Window
    {
        public string CompanyDB => TxtCompanyDb.Text.Trim();
        public string UserName => TxtUsername.Text.Trim();
        public string Password => PwdPassword.Password;

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CompanyDB) || string.IsNullOrWhiteSpace(UserName))
            {
                MessageBox.Show("Please enter both CompanyDB and Username.");
                return;
            }
            DialogResult = true;
        }
    }
}
