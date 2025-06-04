// File: Models/JobLogEntry.cs
using System;

namespace SimplifyQuoter.Models
{
    /// <summary>
    /// Represents a single “job run” (e.g. the user clicked Confirm & Process,
    /// and we inserted/updated X cells).  Mirrors the `job_log` table.
    /// </summary>
    public class JobLogEntry
    {
        /// <summary>
        /// Primary key (UUID).  If not set explicitly, the DB will generate one.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Who ran this job? (e.g. the user‐ID they typed on the login screen).
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The name (or path) of the file that was uploaded for this job.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// What kind of job was this? (Example: “ItemMasterImport”)
        /// </summary>
        public string JobType { get; set; }

        /// <summary>
        /// When did the job start?  (We’ll set this to NOW() on INSERT.)
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When did the job finish?  (We’ll update this after processing.)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// How many cells (or rows) did the user ask us to process?
        /// </summary>
        public int TotalCells { get; set; }

        /// <summary>
        /// Of those TotalCells, how many succeeded?
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Of those TotalCells, how many failed?
        /// </summary>
        public int FailureCount { get; set; }
    }
}
