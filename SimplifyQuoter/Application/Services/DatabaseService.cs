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
