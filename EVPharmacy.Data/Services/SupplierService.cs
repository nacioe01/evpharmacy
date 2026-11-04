
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

public sealed record SupplierInput(
    string? Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address);

/// <summary>Supplier directory. Suppliers are deactivated, never deleted.</summary>
public class SupplierService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SupplierService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Supplier>> SearchAsync(string? search = null, bool includeInactive = false)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.Suppliers.AsNoTracking().Where(s => includeInactive || s.IsActive);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(s =>
                EF.Functions.Like(s.Name, pattern, "\\") ||
                (s.ContactPerson != null && EF.Functions.Like(s.ContactPerson, pattern, "\\")) ||
                (s.Phone != null && EF.Functions.Like(s.Phone, pattern, "\\")));
        }

        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Supplier?> GetAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
    }

    public async Task<OperationResult> CreateAsync(SupplierInput input)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var name = input.Name!.Trim();
        if (await db.Suppliers.AnyAsync(s => s.Name == name))
        {
            return OperationResult.Fail($"A supplier named \"{name}\" already exists.");
        }

        var supplier = new Supplier();
        Apply(supplier, input);
        db.Suppliers.Add(supplier);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail($"A supplier named \"{name}\" already exists.");
        }

        return OperationResult.Ok(supplier.Id);
    }

    public async Task<OperationResult> UpdateAsync(int id, SupplierInput input)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return OperationResult.Fail("This supplier no longer exists.");
        }

        var name = input.Name!.Trim();
        if (await db.Suppliers.AnyAsync(s => s.Id != id && s.Name == name))
        {
            return OperationResult.Fail($"A supplier named \"{name}\" already exists.");
        }

        Apply(supplier, input);
        supplier.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail($"A supplier named \"{name}\" already exists.");
        }

        return OperationResult.Ok(id);
    }

    public async Task<OperationResult> SetActiveAsync(int id, bool isActive)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return OperationResult.Fail("This supplier no longer exists.");
        }

        supplier.IsActive = isActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return OperationResult.Ok(id);
    }

    public static string? Validate(SupplierInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return "Supplier name is required.";
        if (input.Name.Trim().Length > Supplier.MaxNameLength)
            return $"Supplier name must be {Supplier.MaxNameLength} characters or fewer.";
        if (Clean(input.ContactPerson)?.Length > Supplier.MaxContactLength)
            return $"Contact person must be {Supplier.MaxContactLength} characters or fewer.";
        if (Clean(input.Phone)?.Length > Supplier.MaxPhoneLength)
            return $"Phone must be {Supplier.MaxPhoneLength} characters or fewer.";
        if (Clean(input.Address)?.Length > Supplier.MaxAddressLength)
            return $"Address must be {Supplier.MaxAddressLength} characters or fewer.";

        var email = Clean(input.Email);
        if (email is not null)
        {
            if (email.Length > Supplier.MaxEmailLength)
                return $"Email must be {Supplier.MaxEmailLength} characters or fewer.";

            var at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || !email[(at + 1)..].Contains('.') ||
                email.Any(char.IsWhiteSpace))
                return "Enter a valid email address, or leave it empty.";
        }

        return null;
    }

    private static void Apply(Supplier supplier, SupplierInput input)
    {
        supplier.Name = input.Name!.Trim();
        supplier.ContactPerson = Clean(input.ContactPerson);
        supplier.Phone = Clean(input.Phone);
        supplier.Email = Clean(input.Email);
        supplier.Address = Clean(input.Address);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
