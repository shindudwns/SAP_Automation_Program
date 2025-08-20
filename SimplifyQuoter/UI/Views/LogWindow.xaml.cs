// File: Views/LogWindow.xaml.cs

using System;
using System.Collections.ObjectModel;   // [NEW]
using System.IO;                        // [NEW]
using System.Linq;                      // [NEW]
using System.Text;                      // [NEW]
using System.Windows;
using System.Windows.Controls;          // [NEW] TextBox, etc.
using System.Windows.Threading;         // [NEW] DispatcherTimer
using Microsoft.Win32;                  // [NEW] SaveFileDialog
using SimplifyQuoter.Models;            // [NEW] UserEventLog
using SimplifyQuoter.Services;          // [NEW] DatabaseService

// using System.Text;                   // [CHANGED-COMMENTED-OUT] 위에서 이미 추가됨(중복 using)
// using Elmah.ContentSyndication;      // [CHANGED-COMMENTED-OUT] 본 파일에 불필요한 참조라 제거

namespace SimplifyQuoter.Views
{
    // ─────────────────────────────────────────────────────────────────────────
    // [CHANGED-COMMENTED-OUT]
    // 이유: 아래의 메서드들(ApplyPreset/BtnRange…/BtnExportCsv_Click/Csv)이
    //       클래스 밖(네임스페이스 내부 최상위)에 선언돼 있어 컴파일 오류(CS0116)가 발생합니다.
    //       C#에서는 모든 메서드는 클래스 내부에 있어야 하므로, 동일 구현을
    //       아래 LogWindow 클래스 내부로 "추가"합니다.
    //
    // private void ApplyPreset(int? days) { ... }
    // private void BtnRange1d_Click(object sender, RoutedEventArgs e) => ApplyPreset(1);
    // private void BtnRange7d_Click(object sender, RoutedEventArgs e) => ApplyPreset(7);
    // private void BtnRange30d_Click(object sender, RoutedEventArgs e) => ApplyPreset(30);
    // private void BtnRangeAll_Click(object sender, RoutedEventArgs e) => ApplyPreset(null);
    // private void BtnExportCsv_Click(object sender, RoutedEventArgs e) { ... }
    // private static string Csv(object value) { ... }
    // ─────────────────────────────────────────────────────────────────────────

    public partial class LogWindow : Window
    {
        // [NEW] 화면 바인딩 소스 & 타이머
        private readonly ObservableCollection<UserEventLog> _items = new ObservableCollection<UserEventLog>();
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public LogWindow()
        {
            InitializeComponent();

            // [NEW] DataGrid 바인딩 (XAML에 x:Name="GridLogs" 가정)
            GridLogs.ItemsSource = _items;

            TxtUser.Text = string.Empty;
            //// [NEW] 기본 사용자 자동 채움 (XAML에 x:Name="TxtUser" 가정)
            //if (string.IsNullOrWhiteSpace(TxtUser.Text))
            //    TxtUser.Text = (Application.Current.Properties["CurrentUser"] as string ?? "").Trim();

            // [NEW] 들어오자마자 "최근 1일" 자동 로드
            ApplyPreset(1);

            // [NEW] 자동 새로고침(옵션: 주석 처리 가능)
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Tick += (_, __) => LoadLogs();
            this.Closed += (_, __) => { try { _timer.Stop(); } catch { } };
        }
        // [NEW] XAML 이벤트 핸들러 2개
        private void ChkAuto_Checked(object sender, RoutedEventArgs e)
        {
            try { _timer.Start(); } catch { /* no-op */ }
        }

        private void ChkAuto_Unchecked(object sender, RoutedEventArgs e)
        {
            try { _timer.Stop(); } catch { /* no-op */ }
        }
        // [NEW] 기간 프리셋 헬퍼 (days=null이면 전체)
        private void ApplyPreset(int? days)
        {
            //if (string.IsNullOrWhiteSpace(TxtUser.Text))
            //    TxtUser.Text = (Application.Current.Properties["CurrentUser"] as string ?? "").Trim(); 자동으로 현재 사용자 설정 없애기

            if (days.HasValue)
            {
                var today = DateTime.Today;
                var to = today.AddDays(1);                     // 내일 00:00
                var from = today.AddDays(-(days.Value - 1));     // n일 전 00:00
                DpFrom.SelectedDate = from;                      // XAML: x:Name="DpFrom"
                DpTo.SelectedDate = to;                        // XAML: x:Name="DpTo"
            }
            else
            {
                DpFrom.SelectedDate = null;
                DpTo.SelectedDate = null;
            }
            LoadLogs();
        }

        // [NEW] DataGrid 로딩 (이미 동일 메서드가 있으면, 그건 주석 처리하고 이유 남겨 주세요)
        private void LoadLogs()
        {
            try
            {
                var user = string.IsNullOrWhiteSpace(TxtUser.Text) ? null : TxtUser.Text.Trim();
                DateTime? from = DpFrom.SelectedDate;
                DateTime? to = DpTo.SelectedDate;

                using (var db = new DatabaseService())
                {
                    var rows = db.GetEventLogs(user, from, to, limit: 500);
                    _items.Clear();
                    foreach (var r in rows) _items.Add(r);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load logs.\n{ex.Message}", "Log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // [NEW] 최근 1일/7일/30일/전체 버튼 핸들러
        private void BtnRange1d_Click(object sender, RoutedEventArgs e) => ApplyPreset(1);
        private void BtnRange7d_Click(object sender, RoutedEventArgs e) => ApplyPreset(7);
        private void BtnRange30d_Click(object sender, RoutedEventArgs e) => ApplyPreset(30);
        private void BtnRangeAll_Click(object sender, RoutedEventArgs e) => ApplyPreset(null);

        // [NEW] CSV 다운로드
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };
                if (dlg.ShowDialog(this) == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ts,user_id,event,meta,machine,ip");

                    foreach (var r in _items)
                    {
                        sb.AppendLine(string.Join(",",
                            Csv(r.Ts),
                            Csv(r.UserId),
                            Csv(r.Event),
                            Csv(r.MetaJson),
                            Csv(r.Machine),
                            Csv(r.IpAddress)
                        ));
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("CSV로 저장했습니다.", "Log", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 저장 실패\n{ex.Message}", "Log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // [NEW] CSV 셀 포맷터(따옴표 이스케이프)
        private static string Csv(object value)
        {
            var s = value == null ? "" : value.ToString();
            if (s != null) s = s.Replace("\"", "\"\"");
            return "\"" + s + "\"";
        }
    }
}
