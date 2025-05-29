// File: Services/DatabaseService.cs
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Npgsql;

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
