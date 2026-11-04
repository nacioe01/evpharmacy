using Microsoft.Data.Sqlite;

namespace EVPharmacy.Data.Database;

public sealed record BackupInfo(string Path, string FileName, DateTime CreatedAt, long SizeBytes, string Kind);

public class DatabaseBackupService
{
    /// <summary>Automatic backups are kept this many days' worth; older automatic ones are removed.</summary>
    public const int AutomaticBackupsToKeep = 14;

    private const string AutomaticReason = "auto";

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

        // Never reuse a name: two backups in the same second must not overwrite each other.
        var stamp = $"pharmacy_{DateTime.Now:yyyyMMdd_HHmmss}_{safeReason}";
        var backupPath = Path.Combine(AppDatabase.BackupDirectory, stamp + ".db");
        for (var n = 2; File.Exists(backupPath); n++)
        {
            backupPath = Path.Combine(AppDatabase.BackupDirectory, $"{stamp}-{n}.db");
        }

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

    /// <summary>All backup files, newest first.</summary>
    public IReadOnlyList<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(AppDatabase.BackupDirectory))
        {
            return [];
        }

        return new DirectoryInfo(AppDatabase.BackupDirectory)
            .GetFiles("pharmacy_*.db")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupInfo(f.FullName, f.Name, f.LastWriteTime, f.Length, KindOf(f.Name)))
            .ToList();
    }

    /// <summary>
    /// Makes one automatic backup per day (when the app starts) and removes the oldest
    /// automatic ones beyond <see cref="AutomaticBackupsToKeep"/>. Manual, pre-update and
    /// pre-restore backups are never removed automatically. Returns the new path, or null.
    /// </summary>
    public string? CreateAutomaticBackupIfDue()
    {
        if (!File.Exists(AppDatabase.DatabasePath) || new FileInfo(AppDatabase.DatabasePath).Length == 0)
        {
            return null;
        }

        var automatic = ListBackups().Where(b => b.Kind == "Automatic").ToList();
        string? created = null;

        if (automatic.Count == 0 || automatic[0].CreatedAt < DateTime.Now.AddHours(-20))
        {
            created = CreateBackup(AutomaticReason);
        }

        foreach (var old in ListBackups().Where(b => b.Kind == "Automatic").Skip(AutomaticBackupsToKeep))
        {
            TryDelete(old.Path);
        }

        return created;
    }

    /// <summary>
    /// Checks that a file is a usable pharmacy database made by this app (or an older
    /// version of it). Returns a plain-language problem, or null when it can be restored.
    /// </summary>
    public string? ValidateBackup(string path, IReadOnlyCollection<string> knownMigrations)
    {
        if (!File.Exists(path))
        {
            return "That file could not be found.";
        }

        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();

            using var connection = new SqliteConnection(cs);
            connection.Open();

            using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = integrity.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return "That file is damaged and cannot be restored.";
                }
            }

            foreach (var table in new[] { "users", "medicines", "__EFMigrationsHistory" })
            {
                using var exists = connection.CreateCommand();
                exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
                exists.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
                {
                    return "That file is not an EV Pharmacy database.";
                }
            }

            using var history = connection.CreateCommand();
            history.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
            using var reader = history.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                if (!knownMigrations.Contains(id))
                {
                    return "That backup was made by a newer version of EV Pharmacy. Update the application first.";
                }
            }
        }
        catch (SqliteException)
        {
            return "That file is not a valid database.";
        }

        return null;
    }

    /// <summary>
    /// Replaces the live database with the contents of <paramref name="path"/>. The current
    /// database is backed up first. Call <see cref="ValidateBackup"/> before this, and
    /// restart the application afterwards. Returns the path of the safety backup.
    /// </summary>
    public string Restore(string path)
    {
        var safetyBackup = CreateBackup("before-restore");

        // Let go of any idle pooled connections so the file can be overwritten.
        SqliteConnection.ClearAllPools();

        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        using var destination = new SqliteConnection(AppDatabase.BuildConnectionString(pooling: false));

        source.Open();
        destination.Open();
        source.BackupDatabase(destination);

        return safetyBackup;
    }

    private static string KindOf(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        if (name.Contains("_auto")) return "Automatic";
        if (name.Contains("before-restore")) return "Before restore";
        if (name.Contains("pre-migration")) return "Before update";
        if (name.Contains("manual")) return "Manual";
        return "Other";
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A locked or missing file is simply left for next time.
        }
    }
}
