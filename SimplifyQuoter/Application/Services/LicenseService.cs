using System;
using System.Configuration;
using Npgsql;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Handles looking up license codes and logging acceptances.
    /// </summary>
    public class LicenseService : IDisposable
    {
        private readonly NpgsqlConnection _conn;

        public LicenseService()
        {
            var cs = ConfigurationManager
                        .ConnectionStrings["DefaultConnection"]
                        .ConnectionString;
            _conn = new NpgsqlConnection(cs);
            _conn.Open();
        }

        /// <summary>
        /// Checks public.license for the given code.
        /// Returns false if not found, inactive, or expired.
        /// </summary>
        public bool TryGetLicense(
            string code,
            out string companyName,
            out DateTime? validUntil)
        {
            companyName = null;
            validUntil = null;

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT company_name, valid_until, is_active
  FROM public.license
 WHERE code = @code";
                cmd.Parameters.AddWithValue("code", code);

                using (var rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read())
                        return false;               // no such code

                    if (!rdr.GetBoolean(2))
                        return false;               // inactive

                    companyName = rdr.GetString(0);

                    if (!rdr.IsDBNull(1))
                        validUntil = rdr.GetDateTime(1);

                    // if valid_until is set and in the past, reject
                    if (validUntil.HasValue && validUntil.Value < DateTime.UtcNow)
                        return false;               // expired

                    return true;                    // OK!
                }
            }
        }

        /// <summary>
        /// Inserts a row into acceptance_log.
        /// </summary>
        public void LogAcceptance(
            string ipAddress,
            DateTime timestamp,
            string version,
            string deviceInfo,
            string licenseCode,
            bool licenseAccept,
            string companyName)
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO acceptance_log(
  ip_address, accepted_at, agreement_version,
  device_info, license_code, license_accept,
  company_name
) VALUES (
  @ip, @at, @ver,
  @dev, @code, @acc,
  @cmp
)";
                cmd.Parameters.AddWithValue("ip", ipAddress);
                cmd.Parameters.AddWithValue("at", timestamp);
                cmd.Parameters.AddWithValue("ver", version);
                cmd.Parameters.AddWithValue("dev", deviceInfo);
                cmd.Parameters.AddWithValue("code", licenseCode);
                cmd.Parameters.AddWithValue("acc", licenseAccept);
                cmd.Parameters.AddWithValue("cmp", companyName);
                cmd.ExecuteNonQuery();
            }
        }

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
