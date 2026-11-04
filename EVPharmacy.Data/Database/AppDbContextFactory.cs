
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVPharmacy.Data.Database;

/// <summary>
/// Used only by EF Core tooling (migrations) at design time.
/// </summary>
public class AppDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppDatabase.EnsureDirectories();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDatabase.Configure(optionsBuilder);

        return new AppDbContext(optionsBuilder.Options);
    }
}
