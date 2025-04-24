using System;
using System.Configuration;
using Npgsql;

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Encapsulates opening/closing a PostgreSQL connection.
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

        public NpgsqlConnection Connection => _conn;

        public void Dispose()
        {
            _conn?.Close();
            _conn?.Dispose();
        }
    }
}
