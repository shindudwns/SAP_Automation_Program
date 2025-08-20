using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using SimplifyQuoter.Services;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Views
{
    public partial class LogWindow : Window
    {
        private DispatcherTimer _timer;

        public LogWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 기본 기간: 최근 7일
            SetRange(DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1)); // to는 익일 0시(미만)
            RefreshAll();
        }

        // ====== 상단 버튼 이벤트 ======

        private void BtnRange1d_Click(object sender, RoutedEventArgs e)
        {
            SetRange(DateTime.Today, DateTime.Today.AddDays(1));
            RefreshAll();
        }

        private void BtnRange7d_Click(object sender, RoutedEventArgs e)
        {
            SetRange(DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1));
            RefreshAll();
        }

        private void BtnRange30d_Click(object sender, RoutedEventArgs e)
        {
            SetRange(DateTime.Today.AddDays(-29), DateTime.Today.AddDays(1));
            RefreshAll();
        }

        private void BtnRangeAll_Click(object sender, RoutedEventArgs e)
        {
            // 전체: 날짜 필터 해제
            DpFrom.SelectedDate = null;
            DpTo.SelectedDate = null;
            RefreshAll();
        }

        private void ChkAuto_Checked(object sender, RoutedEventArgs e)
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0) };
                _timer.Tick += (_, __) => RefreshAll();
            }
            _timer.Start();
        }

        private void ChkAuto_Unchecked(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            // (원하면 여기서 GridLogs를 CSV로 저장하도록 구현)
            MessageBox.Show("CSV 내보내기는 다음 단계에서 이어서 구현할게요!");
        }

        // ====== 내부 헬퍼 ======

        private void SetRange(DateTime? from, DateTime? toExclusive)
        {
            DpFrom.SelectedDate = from;
            DpTo.SelectedDate = toExclusive?.AddDays(-1); // DatePicker는 날짜 단위니까 UI는 (포함)으로 보이게
        }

        private void GetRange(out DateTime? from, out DateTime? toExclusive)
        {
            from = DpFrom.SelectedDate;
            // 우리가 DB에 넘길 때는 'ts < to' 조건이므로, UI의 'To' 날짜의 다음날 0시를 toExclusive로 사용
            toExclusive = DpTo.SelectedDate.HasValue
                ? DpTo.SelectedDate.Value.Date.AddDays(1)
                : (DateTime?)null;
        }

        private void RefreshAll()
        {
            try
            {
                var user = (TxtUser.Text ?? string.Empty).Trim();
                GetRange(out DateTime? from, out DateTime? to);

                using (var db = new DatabaseService())
                {
                    // 탭 #0: 원본 로그
                    GridLogs.ItemsSource = db.GetEventLogs(user, from, to, 1000);

                    // 탭 #1: 일자 × 사용자
                    GridGptDaily.ItemsSource = db.GetGptUsageDaily(user, from, to);

                    // 탭 #2: 사용자 TOP
                    GridGptTop.ItemsSource = db.GetGptTopUsers(from, to, 10);

                    // 탭 #3: 기능별
                    GridGptFeature.ItemsSource = db.GetGptUsageByFeature(user, from, to);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("로그 로딩 중 오류: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
