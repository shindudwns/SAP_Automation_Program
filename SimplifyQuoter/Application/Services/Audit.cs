// File: Services/Audit.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace SimplifyQuoter.Services
{
    public static class Audit
    {

        // [NEW] 로컬 IP 안전 조회 (LoginWindow에 있는 것과 같은 로직을 여기에도 내장)
        private static string GetLocalIpSafe()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var a in host.AddressList)
                    if (a.AddressFamily == AddressFamily.InterNetwork)
                        return a.ToString();
            }
            catch { }
            return null;
        }

        // [NEW] GPT 사용 로그(한 줄)
        //  - event: "gpt_usage"
        //  - user:  지정 없으면 CurrentUser 사용
        //  - meta:  기능명/모델/토큰/파라미터 등을 JSON으로 저장
        public static void LogGptUsage(
            string feature,
            int promptTokens,
            int completionTokens,
            int totalTokens,
            string model,
            string user,
            string partCode = null,
            int? items = null,
            string ip = null)
        {
            var meta = new
            {
                feature = feature,
                model = model,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = totalTokens,
                part_code = partCode,
                items = items
            };

            using (var db = new DatabaseService())
            {
                // user_event_log에 기록 (event = "gpt_usage")
                db.LogEvent(user ?? "", "gpt_usage", meta, Environment.MachineName, ip);
            }


            LogWithIp(
      "gpt_usage",
      meta,
      user: string.IsNullOrWhiteSpace(user)
              ? ((Application.Current.Properties["CurrentUser"] as string) ?? "").Trim()
              : user,
      ip: GetLocalIpSafe()
  );
        }
        // [NEW] GPT 오류 로그(옵션)
        public static void LogGptError(
            string feature, string message, string user = null, string partCode = null, object extra = null)
        {
            var meta = new
            {
                feature = feature,
                error = message,
                part_code = partCode,
                extra = extra,
                companyDb = (Application.Current.Properties["CompanyDB"] as string)
            };

            LogWithIp("gpt_error", meta,
                user: string.IsNullOrWhiteSpace(user)
                        ? ((Application.Current.Properties["CurrentUser"] as string) ?? "").Trim()
                        : user,
                ip: GetLocalIpSafe());
        }


        public static void EnsureTable()
        {
            try
            {
                using (var db = new DatabaseService())
                    db.EnsureUserEventLogTable();
            }
            catch { /* no-op */ }
        }

        // [NEW] 이벤트 기록 (user 미지정 시 Application.Current.Properties["CurrentUser"] 사용)
        public static void Log(string evt, object meta = null, string user = null)
        {
            try
            {
                var uid = string.IsNullOrWhiteSpace(user)
                    ? ((Application.Current.Properties["CurrentUser"] as string) ?? "").Trim()
                    : user;

                using (var db = new DatabaseService())
                    db.LogEvent(uid, evt, meta, Environment.MachineName, null);
            }
            catch { /* no-op */ }
        }
        // [NEW] IP를 별도 컬럼으로 기록하는 버전
        public static void LogWithIp(string evt, object meta = null, string user = null, string ip = null)
        {
            try
            {
                var uid = string.IsNullOrWhiteSpace(user)
                    ? ((Application.Current.Properties["CurrentUser"] as string) ?? "").Trim()
                    : user;

                using (var db = new DatabaseService())
                    db.LogEvent(uid, evt, meta, Environment.MachineName, ip); // ← machine은 자동, ip는 인자 전달
            }
            catch { /* no-op */ }
        }
    }
}

