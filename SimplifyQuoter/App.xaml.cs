// File: App.xaml.cs
using System.Diagnostics;
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Views;

namespace SimplifyQuoter
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1) Create MainWindow but do NOT call Show() on it yet:
            var mainWin = new MainWindow();
            this.MainWindow = mainWin;
            mainWin.Visibility = Visibility.Collapsed;

            // 2) Show LoginWindow modally:
            var loginWin = new LoginWindow();
            bool? loginResult = loginWin.ShowDialog();
            Debug.WriteLine($"[App] LoginWindow returned {loginResult}.");

            // 3) If login succeeded, save the client & username, then show MainWindow
            if (loginResult == true)
            {
                // *** Store the logged-in ServiceLayerClient & UserName in our shared state ***
                var state = AutomationWizardState.Current;
                state.SlClient = loginWin.SlClient;
                state.UserName = loginWin.UserName;

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
