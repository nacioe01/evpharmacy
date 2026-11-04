
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Database;

/// <summary>
/// Brings the database schema up to date at startup.
/// An existing database is always backed up before any migration is applied.
/// </summary>
public class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DatabaseBackupService _backupService;

    public DatabaseInitializer(
        IDbContextFactory<AppDbContext> contextFactory,
        DatabaseBackupService backupService)
    {
        _contextFactory = contextFactory;
        _backupService = backupService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppDatabase.EnsureDirectories();

        await using var dbContext =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var pending = await dbContext.Database
            .GetPendingMigrationsAsync(cancellationToken);

        if (!pending.Any())
        {
            return;
        }

        var databaseExists = File.Exists(AppDatabase.DatabasePath) &&
                             new FileInfo(AppDatabase.DatabasePath).Length > 0;

        if (databaseExists)
        {
            _backupService.CreateBackup("pre-migration");
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
