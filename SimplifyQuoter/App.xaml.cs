// File: App.xaml.cs
using System;
using System.Diagnostics;
using System.Linq;                // [NEW]
using System.Net;                 // [NEW]
using System.Net.Sockets;         // [NEW]
using System.Windows;
using SimplifyQuoter.Models;
using SimplifyQuoter.Views;
using SimplifyQuoter.Services;    // [NEW] DatabaseService 사용

namespace SimplifyQuoter
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var mainWin = new MainWindow();
            this.MainWindow = mainWin;
            mainWin.Visibility = Visibility.Collapsed;

            var loginWin = new LoginWindow();
            bool? loginResult = loginWin.ShowDialog();
            Debug.WriteLine($"[App] LoginWindow returned {loginResult}.");

            if (loginResult == true)
            {
                var state = AutomationWizardState.Current;
                state.SlClient = loginWin.SlClient;
                state.UserName = loginWin.UserName;

                // [NEW] 표준화된 "login" 이벤트 로그 남기기
                try
                {
                    using (var db = new DatabaseService())
                    {
                        db.EnsureUserEventLogTable();
                        db.LogEvent(
                            userId: state.UserName ?? string.Empty,
                            @event: "login",                               // ← 반드시 소문자 login
                            meta: new
                            {
                                feature = "auth",
                                action = "login",
                                machine = Environment.MachineName
                            },
                            machine: Environment.MachineName,
                            ip: GetLocalIPv4()
                        );
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine("[App] login log failed: " + ex);
                }

                // [NEW] 앱 종료 시 logout 기록
                this.Exit += (s, args) =>
                {
                    try
                    {
                        using (var db = new DatabaseService())
                        {
                            db.LogEvent(
                                userId: AutomationWizardState.Current?.UserName ?? string.Empty,
                                @event: "logout",
                                meta: new { feature = "auth", action = "logout" },
                                machine: Environment.MachineName,
                                ip: GetLocalIPv4()
                            );
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.WriteLine("[App] logout log failed: " + ex);
                    }
                };

                Debug.WriteLine("[App] Showing MainWindow now.");
                mainWin.Visibility = Visibility.Visible;
            }
            else
            {
                 try
                {
                    using (var db = new DatabaseService())
                        db.LogEvent("login_failed", "login_fail", new { reason = "cancel" }, Environment.MachineName, GetLocalIPv4());
                }
                catch { }
                Debug.WriteLine("[App] Login canceled/failed. Shutting down.");
                Shutdown();
            }
        }

        // [NEW] 간단한 로컬 IPv4 추출
        private static string GetLocalIPv4()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                return ip?.ToString();
            }
            catch { return null; }
        }
    }
}
