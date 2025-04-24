using System;
using System.Collections.Generic;
using Npgsql;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    public class AutomationService
    {
        public void Connect()
        {
            // TODO: DI‐API connect
        }

        public void Disconnect()
        {
            // TODO: DI‐API disconnect
        }

        public void RunItemMasterData(Guid sapFileId, IEnumerable<RowView> rows)
        {
            // insert process_job
            Guid jobId;
            int total = 0;
            foreach (var _ in rows) total++;
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(sap_file_id,job_type,total_rows)
VALUES(@fid,'IMD',@tot) RETURNING id";
                cmd.Parameters.AddWithValue("fid", sapFileId);
                cmd.Parameters.AddWithValue("tot", total);
                jobId = (Guid)cmd.ExecuteScalar();
            }

            Connect();
            foreach (var rv in rows)
            {
                // TODO: call DI-API to create Item Master Data…

                using (var db = new DatabaseService())
                using (var tx = db.Connection.BeginTransaction())
                {
                    // mark row processed
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE sap_row
   SET processed_imd = TRUE,
       imd_exec_count = imd_exec_count + 1
 WHERE id = @rid";
                        cmd.Parameters.AddWithValue("rid", rv.RowId);
                        cmd.ExecuteNonQuery();
                    }
                    // bump job
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                        cmd.Parameters.AddWithValue("jid", jobId);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            Disconnect();

            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE process_job
   SET completed_at = NOW()
 WHERE id = @jid";
                cmd.Parameters.AddWithValue("jid", jobId);
                cmd.ExecuteNonQuery();
            }
        }

        public void RunSalesQuotation(Guid sapFileId, IEnumerable<RowView> rows)
        {
            Guid jobId;
            int total = 0;
            foreach (var _ in rows) total++;
            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO process_job(sap_file_id,job_type,total_rows)
VALUES(@fid,'SQ',@tot) RETURNING id";
                cmd.Parameters.AddWithValue("fid", sapFileId);
                cmd.Parameters.AddWithValue("tot", total);
                jobId = (Guid)cmd.ExecuteScalar();
            }

            Connect();
            foreach (var rv in rows)
            {
                // TODO: call DI-API to create Sales Quotation…

                using (var db = new DatabaseService())
                using (var tx = db.Connection.BeginTransaction())
                {
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE sap_row
   SET processed_sq = TRUE,
       sq_exec_count = sq_exec_count + 1
 WHERE id = @rid";
                        cmd.Parameters.AddWithValue("rid", rv.RowId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE process_job
   SET processed_rows = processed_rows + 1
 WHERE id = @jid";
                        cmd.Parameters.AddWithValue("jid", jobId);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            Disconnect();

            using (var db = new DatabaseService())
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE process_job
   SET completed_at = NOW()
 WHERE id = @jid";
                cmd.Parameters.AddWithValue("jid", jobId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
