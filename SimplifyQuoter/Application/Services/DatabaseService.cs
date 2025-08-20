// File: Services/DatabaseService.cs
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Npgsql;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{


    /// <summary>
    /// Wraps an open NpgsqlConnection, plus helpers for
    /// part-cache and job/row flag updates.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly NpgsqlConnection _conn;

        public DatabaseService()
        {
            var cs = ConfigurationManager
                        .ConnectionStrings["DefaultConnection"]
                        .ConnectionString;
            _conn = new NpgsqlConnection(cs);
            _conn.Open();
        }

        /// <summary>
        /// Expose raw connection if needed.
        /// </summary>
        public NpgsqlConnection Connection
        {
            get { return _conn; }
        }

        /// <summary>
        /// Checks if the given license code exists in the "license" table,
        /// is marked active, and is currently within its valid date range.
        /// </summary>
        public bool IsLicenseCodeValid(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT 1
  FROM license
 WHERE code = @code
   AND is_active = TRUE
   AND (valid_from <= NOW())
   AND (
         valid_until IS NULL 
         OR valid_until >= NOW()
       )
 LIMIT 1;
";
                cmd.Parameters.AddWithValue("code", code.Trim());
                var result = cmd.ExecuteScalar();
                return (result != null);
            }
        }



        /// <summary>
        /// Inserts a new row into "acceptance_log", using the given licenseCode.
        /// It first retrieves the matching company_name from the license table,
        /// then writes:
        ///   ip_address, accepted_at (NOW()), agreement_version, device_info,
        ///   license_code, license_accept, company_name
        /// </summary>
        public void LogAcceptance(
    string userId,             
    string licenseCode,
    bool licenseAccept,
    string agreementVersion,
    string deviceInfo,
    string ipAddress)
        {
            // 1) Look up company_name from the license table
            string companyName = string.Empty;
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT company_name
  FROM license
 WHERE code = @code;
";
                cmd.Parameters.AddWithValue("code", licenseCode.Trim());
                var o = cmd.ExecuteScalar();
                if (o != null && o != DBNull.Value)
                    companyName = (string)o;
            }

            // 2) Insert into acceptance_log (including the new user_id)
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO acceptance_log (
    user_id,
    ip_address,
    accepted_at,
    agreement_version,
    device_info,
    license_code,
    license_accept,
    company_name
) VALUES (
    @user, @ip, NOW(), @ver, @device, @code, @accept, @company
);
";
                // Add the new parameter first:
                cmd.Parameters.AddWithValue("user", userId ?? string.Empty);
                cmd.Parameters.AddWithValue("ip", (object)ipAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ver", agreementVersion ?? string.Empty);
                cmd.Parameters.AddWithValue("device", (object)deviceInfo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("code", licenseCode.Trim());
                cmd.Parameters.AddWithValue("accept", licenseAccept);
                cmd.Parameters.AddWithValue("company", companyName ?? string.Empty);

                cmd.ExecuteNonQuery();
            }
        }


        // ————— Existing methods —————

        public HashSet<string> GetKnownPartCodes(IEnumerable<string> codes)
        {
            var arr = (codes ?? Enumerable.Empty<string>())
                      .Where(c => !string.IsNullOrWhiteSpace(c))
                      .Select(c => c.Trim())
                      .ToArray();
            if (arr.Length == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT code FROM part WHERE code = ANY(@codes)";
                cmd.Parameters.AddWithValue(
                    "codes",
                    NpgsqlTypes.NpgsqlDbType.Array |
                    NpgsqlTypes.NpgsqlDbType.Text,
                    arr);

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        set.Add(rdr.GetString(0));
                return set;
            }
        }

        public string GetDescription(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT description
  FROM part
 WHERE code = @code
   AND description IS NOT NULL";
                cmd.Parameters.AddWithValue("code", code.Trim());
                var o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value)
                    ? string.Empty
                    : (string)o;
            }
        }

        public string GetItemGroup(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT item_group
  FROM part
 WHERE code = @code
   AND item_group IS NOT NULL";
                cmd.Parameters.AddWithValue("code", code.Trim());
                var o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value)
                    ? string.Empty
                    : (string)o;
            }
        }

        public void UpsertPart(
            string code,
            string description,
            string itemGroup,
            bool isManual = false)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO part(code, description, item_group, is_manual, enriched_at)
VALUES(@code,@desc,@grp,@man,NOW())
ON CONFLICT(code) DO UPDATE
  SET description   = EXCLUDED.description,
      item_group    = EXCLUDED.item_group,
      enriched_at   = NOW(),
      is_manual     = part.is_manual OR EXCLUDED.is_manual";
                cmd.Parameters.AddWithValue("code", code ?? string.Empty);
                cmd.Parameters.AddWithValue("desc", description ?? string.Empty);
                cmd.Parameters.AddWithValue("grp", itemGroup ?? string.Empty);
                cmd.Parameters.AddWithValue("man", isManual);
                cmd.ExecuteNonQuery();
            }
        }

        public int CleanupParts()
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"DELETE FROM part";
                return cmd.ExecuteNonQuery();
            }
        }

        // ————— New job/row‐flag methods —————

        /// <summary>
        /// Inserts a new process_job and returns its ID.
        /// </summary>
        public Guid CreateProcessJob(Guid sapFileId, string jobType, int totalRows)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(sap_file_id, job_type, total_rows)
VALUES(@fid,@jt,@tot)
RETURNING id";
                cmd.Parameters.AddWithValue("fid", sapFileId);
                cmd.Parameters.AddWithValue("jt", jobType);
                cmd.Parameters.AddWithValue("tot", totalRows);
                return (Guid)cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Increments processed_rows on the given job.
        /// </summary>
        public void IncrementJobProgress(Guid jobId)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                cmd.Parameters.AddWithValue("jid", jobId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Sets completed_at = NOW() on the given job.
        /// </summary>
        public void CompleteJob(Guid jobId)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE process_job
   SET completed_at = NOW()
 WHERE id = @jid";
                cmd.Parameters.AddWithValue("jid", jobId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Marks a sap_row as processed_imd and bumps exec count.
        /// </summary>
        public void MarkImdProcessed(Guid rowId)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE sap_row
   SET processed_imd  = TRUE,
       imd_exec_count = imd_exec_count + 1
 WHERE id = @rid";
                cmd.Parameters.AddWithValue("rid", rowId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Marks a sap_row as processed_sq and bumps exec count.
        /// </summary>
        public void MarkSqProcessed(Guid rowId)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE sap_row
   SET processed_sq  = TRUE,
       sq_exec_count  = sq_exec_count + 1
 WHERE id = @rid";
                cmd.Parameters.AddWithValue("rid", rowId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Insert a new job_log row.  If entry.Id is null, we let the DB generate it.
        /// </summary>
        public void InsertJobLog(JobLogEntry entry)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO job_log (
    id,
    user_id,
    file_name,
    job_type,
    started_at,
    completed_at,
    total_cells,
    success_count,
    failure_count,
    patch_count
) VALUES (
    COALESCE(@id, gen_random_uuid()),
    @user,
    @file,
    @type,
    @start,
    @end,
    @total,
    @succ,
    @fail,
    @patch
);
";

                cmd.Parameters.AddWithValue("id", (object)entry.Id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("user", entry.UserId);
                cmd.Parameters.AddWithValue("file", entry.FileName);
                cmd.Parameters.AddWithValue("type", entry.JobType);
                cmd.Parameters.AddWithValue("start", entry.StartedAt);
                cmd.Parameters.AddWithValue("end", entry.CompletedAt);
                cmd.Parameters.AddWithValue("total", entry.TotalCells);
                cmd.Parameters.AddWithValue("succ", entry.SuccessCount);
                cmd.Parameters.AddWithValue("fail", entry.FailureCount);

                // NEW:
                cmd.Parameters.AddWithValue("patch", entry.PatchCount);

                cmd.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// [NEW] 현재 연결된 서버/DB 정보 확인용 (디버깅용)
        /// </summary>
        /// <summary>
        /// [NEW] 현재 연결된 서버/DB 정보 확인용 (디버깅용)
        /// </summary>
        public (string Host, int? Port, string Db) GetServerInfo()
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "select inet_server_addr()::text, inet_server_port(), current_database();";
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        string host = r.IsDBNull(0) ? null : r.GetString(0);
                        int? port = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
                        string db = r.IsDBNull(2) ? null : r.GetString(2);
                        return (host, port, db);
                    }
                }
            }
            return (null, null, null);
        }

        // [NEW] 안전 Ordinal 헬퍼 (없으면 추가)
        private static bool TryGetOrdinal(System.Data.IDataRecord rec, string name, out int ordinal)
        {
            try { ordinal = rec.GetOrdinal(name); return true; }
            catch { ordinal = -1; return false; }
        }


        // ===== [NEW] Diagnostics & User Event Log =====

        /// [NEW] user_event_log 테이블이 없으면 생성
        /// </summary>
        public void EnsureUserEventLogTable()
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
create table if not exists user_event_log (
  id bigserial primary key,
  ts timestamptz not null default now(),
  user_id text not null,
  event text not null,
  meta jsonb,
  machine text,
  ip_address text
);
create index if not exists ix_user_event_log_ts on user_event_log(ts);
create index if not exists ix_user_event_log_user on user_event_log(user_id);
create index if not exists ix_user_event_log_event on user_event_log(event);";
               cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// [NEW] 사용자 이벤트 로그 INSERT
        ///   meta 객체는 JSON으로 직렬화되어 meta(jsonb)에 저장됨
        /// </summary>
        public void LogEvent(string userId, string @event, object meta = null, string machine = null, string ip = null)
        {
            string metaJson = (meta == null) ? null : Newtonsoft.Json.JsonConvert.SerializeObject(meta);

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
insert into user_event_log(user_id, event, meta, machine, ip_address)
values (@user_id, @event, @meta, @machine, @ip);";

                cmd.Parameters.AddWithValue("user_id", userId ?? string.Empty);
                cmd.Parameters.AddWithValue("event", @event ?? string.Empty);

                var pMeta = new Npgsql.NpgsqlParameter("meta", NpgsqlTypes.NpgsqlDbType.Jsonb);
                pMeta.Value = (object)metaJson ?? DBNull.Value;
                cmd.Parameters.Add(pMeta);

                cmd.Parameters.AddWithValue("machine", (object)machine ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ip", (object)ip ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// [NEW] 사용자 이벤트 로그 SELECT (필터 가능)
        /// </summary>
        // [CHANGED-COMMENTED-OUT - C# 7.3에서는 nullable reference syntax 미지원]
        // public System.Collections.Generic.List<UserEventLog> GetEventLogs(string? userId, DateTime? from, DateTime? to, int limit = 500)
        // { ... }

        // [NEW - C# 7.3 호환 버전]
        public System.Collections.Generic.List<UserEventLog> GetEventLogs(string userId, DateTime? from, DateTime? to, int limit = 500)
        {

            var list = new System.Collections.Generic.List<UserEventLog>();

            var where = new System.Text.StringBuilder("where 1=1 ");
            if (!string.IsNullOrWhiteSpace(userId)) where.Append("and user_id = @user_id ");
            if (from.HasValue) where.Append("and ts >= @from ");
            if (to.HasValue) where.Append("and ts <  @to ");



            using (var cmd = _conn.CreateCommand())
            {
                //                cmd.CommandText = $@"
                //select id, ts, user_id, event, meta::text as meta, machine, ip_address
                //from user_event_log
                //{where}
                //order by ts desc
                //limit @limit;";
                // [NEW] meta(JSON)에서 GPT 필드들을 뽑아 컬럼으로 노출
                cmd.CommandText = $@"
select
  id,
  ts,
  user_id,
  event,
  meta::text as meta,
  machine,
  ip_address,
  nullif(meta->>'prompt_tokens','')::int           as gpt_prompt_tokens,
  nullif(meta->>'completion_tokens','')::int       as gpt_completion_tokens,
  coalesce(
    nullif(meta->>'total_tokens','')::int,
    (nullif(meta->>'prompt_tokens','')::int + nullif(meta->>'completion_tokens','')::int)
  )                                                as gpt_total_tokens,
  meta->>'model'                                   as gpt_model,
  meta->>'feature'                                 as gpt_feature,
  meta->>'part_code'                               as gpt_part_code,
  nullif(meta->>'items','')::int                   as gpt_items
from user_event_log
{where}
order by ts desc
limit @limit;";



                if (!string.IsNullOrWhiteSpace(userId)) cmd.Parameters.AddWithValue("user_id", userId);
                if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
                if (to.HasValue) cmd.Parameters.AddWithValue("to", to.Value);
                cmd.Parameters.AddWithValue("limit", limit);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var item = new UserEventLog
                        {
                            Id = r.GetInt64(0),
                            Ts = r.GetDateTime(1),
                            UserId = r.GetString(2),
                            Event = r.GetString(3),
                            MetaJson = r.IsDBNull(4) ? null : r.GetString(4),
                            Machine = r.IsDBNull(5) ? null : r.GetString(5),
                            IpAddress = r.IsDBNull(6) ? null : r.GetString(6),
                        };

                        // [NEW] GPT 세부 필드 (없으면 NULL)
                        int ord;
                        item.GptPromptTokens = r.IsDBNull(7) ? (int?)null : r.GetInt32(7);
                        item.GptCompletionTokens = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
                        item.GptTotalTokens = r.IsDBNull(9) ? (int?)null : r.GetInt32(9);
                        item.GptModel = r.IsDBNull(10) ? null : r.GetString(10);
                        item.GptFeature = r.IsDBNull(11) ? null : r.GetString(11);
                        item.GptPartCode = r.IsDBNull(12) ? null : r.GetString(12);
                        item.GptItems = r.IsDBNull(13) ? (int?)null : r.GetInt32(13);
                        list.Add(item);
                    }
                }

            }

            return list;
        }

        // ===== [END NEW] =====




        // ————— IDisposable —————



        public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
            }
        }
    }
}
