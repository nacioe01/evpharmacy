
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

/// <summary>One lookup entry plus how many medicines use it.</summary>
public sealed record LookupEntry(int Id, string Name, bool IsActive, int MedicineCount);

/// <summary>
/// Maintains the admin-managed lists (categories, dosage forms, units of measure).
/// Entries are deactivated, never deleted, so older records stay meaningful.
/// </summary>
public class LookupService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public LookupService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<T>> GetAsync<T>(bool includeInactive = false) where T : LookupItem
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Set<T>()
            .AsNoTracking()
            .Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>All entries (active and inactive) with the number of medicines using each.</summary>
    public async Task<List<LookupEntry>> GetEntriesAsync<T>() where T : LookupItem
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var items = await db.Set<T>().AsNoTracking().OrderBy(x => x.Name).ToListAsync();

        var medicines = db.Medicines.AsNoTracking();
        var counts = typeof(T) == typeof(Category)
            ? await medicines.GroupBy(m => m.CategoryId)
                .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count)
            : typeof(T) == typeof(DosageForm)
                ? await medicines.GroupBy(m => m.DosageFormId)
                    .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count)
                : await medicines.GroupBy(m => m.UnitOfMeasureId)
                    .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);

        return items
            .Select(i => new LookupEntry(i.Id, i.Name, i.IsActive, counts.GetValueOrDefault(i.Id)))
            .ToList();
    }

    public async Task<OperationResult> AddAsync<T>(string? name) where T : LookupItem, new()
    {
        var error = ValidateName(name);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var clean = name!.Trim();

        await using var db = await _contextFactory.CreateDbContextAsync();

        // Name column is NOCASE, so this comparison ignores case.
        var existing = await db.Set<T>().FirstOrDefaultAsync(x => x.Name == clean);
        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return OperationResult.Fail($"\"{clean}\" already exists.");
            }

            // Reuse the old entry instead of creating a near-duplicate.
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return OperationResult.Ok(existing.Id);
        }

        var item = new T { Name = clean };
        db.Set<T>().Add(item);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail($"\"{clean}\" already exists.");
        }

        return OperationResult.Ok(item.Id);
    }

    public async Task<OperationResult> RenameAsync<T>(int id, string? name) where T : LookupItem
    {
        var error = ValidateName(name);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var clean = name!.Trim();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var item = await db.Set<T>().FindAsync(id);
        if (item is null)
        {
            return OperationResult.Fail("That entry no longer exists.");
        }

        if (await db.Set<T>().AnyAsync(x => x.Id != id && x.Name == clean))
        {
            return OperationResult.Fail($"\"{clean}\" already exists.");
        }

        item.Name = clean;
        item.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail($"\"{clean}\" already exists.");
        }

        return OperationResult.Ok(id);
    }

    public async Task<OperationResult> SetActiveAsync<T>(int id, bool isActive) where T : LookupItem
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var item = await db.Set<T>().FindAsync(id);
        if (item is null)
        {
            return OperationResult.Fail("That entry no longer exists.");
        }

        item.IsActive = isActive;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return OperationResult.Ok(id);
    }

    public static string? ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return "Name is required.";
        }

        return value.Length > LookupItem.MaxNameLength
            ? $"Name must be {LookupItem.MaxNameLength} characters or fewer."
            : null;
    }
}
