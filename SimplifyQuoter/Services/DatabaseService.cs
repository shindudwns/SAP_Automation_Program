using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Npgsql;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Wraps an open NpgsqlConnection, plus some part-cache helpers.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private NpgsqlConnection _conn;

        public DatabaseService()
        {
            var cs = ConfigurationManager
                        .ConnectionStrings["DefaultConnection"]
                        .ConnectionString;
            _conn = new NpgsqlConnection(cs);
            _conn.Open();
        }

        public NpgsqlConnection Connection
        {
            get { return _conn; }
        }

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
SELECT code
  FROM part
 WHERE code = ANY(@codes)";
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

        /// <summary>
        /// Deletes all rows from the part table.
        /// Returns the number of rows removed.
        /// </summary>
        public int CleanupParts()
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"DELETE FROM part";
                return cmd.ExecuteNonQuery();
            }
        }


        public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
                _conn = null;
            }
        }
    }
}
