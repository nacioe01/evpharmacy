
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Database;

/// <summary>
/// Single source of truth for the database location and connection settings.
/// Used by the application at runtime and by the EF design-time factory.
/// </summary>
public static class AppDatabase
{
    /// <summary>
    /// Optional override for development and testing, so a separate database
    /// can be used without touching the real pharmacy data.
    /// </summary>
    public const string DataDirectoryOverrideVariable = "EVPHARMACY_DATA_DIR";

    public static string DataDirectory { get; } = ResolveDataDirectory();

    private static string ResolveDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable(DataDirectoryOverrideVariable);

        return !string.IsNullOrWhiteSpace(overridePath)
            ? Path.GetFullPath(overridePath)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EVPharmacy");
    }

    public static string DatabasePath => Path.Combine(DataDirectory, "pharmacy.db");

    public static string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }

    public static string BuildConnectionString(string? databasePath = null, bool pooling = true)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Enforce foreign keys explicitly on every connection.
            ForeignKeys = true,
            Pooling = pooling
        }.ToString();
    }

    public static void Configure(DbContextOptionsBuilder options)
    {
        options.UseSqlite(BuildConnectionString());
    }
}
