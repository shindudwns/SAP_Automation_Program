using System;
using System.Configuration;
using Npgsql;

namespace SimplifyQuoter.Services
{
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

        public NpgsqlConnection Connection
        {
            get { return _conn; }
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
