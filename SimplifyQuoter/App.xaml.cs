// File: App.xaml.cs
using System.Diagnostics;
using System.Windows;
using SimplifyQuoter.Views;

namespace SimplifyQuoter
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1) Create MainWindow but do NOT call Show() on it yet:
            var mainWin = new MainWindow();
            this.MainWindow = mainWin;       // tell WPF “this is the app’s main window”
            mainWin.Visibility = Visibility.Collapsed; // keep it invisible for now

            // 2) Show LoginWindow modally:
            var loginWin = new LoginWindow();
            bool? loginResult = loginWin.ShowDialog();
            Debug.WriteLine($"[App] LoginWindow returned {loginResult}.");

            // 3) If login succeeded, show the MainWindow:
            if (loginResult == true)
            {
                Debug.WriteLine("[App] Showing MainWindow now.");
                mainWin.Visibility = Visibility.Visible;
            }
            else
            {
                Debug.WriteLine("[App] Login canceled/failed. Shutting down.");
                Shutdown();
            }
        }
    }
}
