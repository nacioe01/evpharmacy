
using Microsoft.Data.Sqlite;

namespace EVPharmacy.Data.Database;

public class DatabaseBackupService
{
    /// <summary>
    /// Creates a consistent copy of the live database using SQLite's online
    /// backup API (safe with WAL mode and open connections).
    /// Returns the full path of the backup file.
    /// </summary>
    public string CreateBackup(string reason)
    {
        AppDatabase.EnsureDirectories();

        var safeReason = string.Concat(
            reason.Select(c => char.IsLetterOrDigit(c) ? c : '-'));

        var backupPath = Path.Combine(
            AppDatabase.BackupDirectory,
            $"pharmacy_{DateTime.Now:yyyyMMdd_HHmmss}_{safeReason}.db");

        // Pooling disabled so the backup file is released immediately.
        using var source = new SqliteConnection(
            AppDatabase.BuildConnectionString(pooling: false));
        using var destination = new SqliteConnection(
            AppDatabase.BuildConnectionString(backupPath, pooling: false));

        source.Open();
        destination.Open();
        source.BackupDatabase(destination);

        return backupPath;
    }
}
