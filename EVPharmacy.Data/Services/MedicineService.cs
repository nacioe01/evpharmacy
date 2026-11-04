
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

/// <summary>Values entered on the medicine form. Prices are integer centavos.</summary>
public sealed record MedicineInput(
    string? GenericName,
    string? BrandName,
    string? Barcode,
    int CategoryId,
    int DosageFormId,
    string? Strength,
    int UnitOfMeasureId,
    long PurchaseCostCentavos,
    long SellingPriceCentavos,
    int ReorderLevel,
    PrescriptionClass PrescriptionClass,
    bool IsVatExempt,
    string? PriceChangeReason);

public sealed record MedicineListItem(
    int Id,
    string GenericName,
    string? BrandName,
    string? Barcode,
    string? Strength,
    string CategoryName,
    string DosageFormName,
    string UnitName,
    long PurchaseCostCentavos,
    long SellingPriceCentavos,
    int ReorderLevel,
    PrescriptionClass PrescriptionClass,
    bool IsVatExempt,
    bool IsActive);

public sealed record PriceHistoryEntry(
    DateTime ChangedAtUtc,
    long? OldPurchaseCostCentavos,
    long NewPurchaseCostCentavos,
    long? OldSellingPriceCentavos,
    long NewSellingPriceCentavos,
    string? Reason,
    string? ChangedBy);

/// <summary>
/// Medicine master data: create, read, update and deactivate.
/// Medicines are never deleted, and every price change is logged.
/// </summary>
public class MedicineService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MedicineService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<MedicineListItem>> SearchAsync(
        string? search = null,
        bool includeInactive = false)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.Medicines.AsNoTracking()
            .Where(m => includeInactive || m.IsActive);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            // Parameterized by EF. Barcode column is NOCASE; names use LIKE (case-insensitive for ASCII).
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(m =>
                EF.Functions.Like(m.GenericName, pattern, "\\") ||
                (m.BrandName != null && EF.Functions.Like(m.BrandName, pattern, "\\")) ||
                (m.Barcode != null && m.Barcode == term));
        }

        return await query
            .OrderBy(m => m.GenericName).ThenBy(m => m.BrandName)
            .Select(m => new MedicineListItem(
                m.Id, m.GenericName, m.BrandName, m.Barcode, m.Strength,
                m.Category!.Name, m.DosageForm!.Name, m.UnitOfMeasure!.Name,
                m.PurchaseCostCentavos, m.SellingPriceCentavos, m.ReorderLevel,
                m.PrescriptionClass, m.IsVatExempt, m.IsActive))
            .ToListAsync();
    }

    public async Task<Medicine?> GetAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Medicines.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
    }

    public async Task<OperationResult> CreateAsync(MedicineInput input, int? userId)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var barcode = Clean(input.Barcode);
        if (barcode is not null && await db.Medicines.AnyAsync(m => m.Barcode == barcode))
        {
            return OperationResult.Fail("Another medicine already uses this barcode.");
        }

        var medicine = new Medicine { GenericName = input.GenericName!.Trim() };
        Apply(medicine, input, barcode);

        // First price entry (no "old" values) so the log is complete from day one.
        medicine.PriceHistory.Add(new MedicinePriceHistory
        {
            NewPurchaseCostCentavos = input.PurchaseCostCentavos,
            NewSellingPriceCentavos = input.SellingPriceCentavos,
            Reason = "Initial price",
            ChangedByUserId = userId
        });

        db.Medicines.Add(medicine);

        // One SaveChanges is one transaction: the medicine and its first price entry together.
        return await SaveAsync(db, () => medicine.Id);
    }

    public async Task<OperationResult> UpdateAsync(int id, MedicineInput input, int? userId)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var medicine = await db.Medicines.SingleOrDefaultAsync(m => m.Id == id);
        if (medicine is null)
        {
            return OperationResult.Fail("This medicine no longer exists.");
        }

        var barcode = Clean(input.Barcode);
        if (barcode is not null &&
            await db.Medicines.AnyAsync(m => m.Id != id && m.Barcode == barcode))
        {
            return OperationResult.Fail("Another medicine already uses this barcode.");
        }

        var priceChanged =
            medicine.PurchaseCostCentavos != input.PurchaseCostCentavos ||
            medicine.SellingPriceCentavos != input.SellingPriceCentavos;

        if (priceChanged)
        {
            db.MedicinePriceHistories.Add(new MedicinePriceHistory
            {
                MedicineId = id,
                OldPurchaseCostCentavos = medicine.PurchaseCostCentavos,
                NewPurchaseCostCentavos = input.PurchaseCostCentavos,
                OldSellingPriceCentavos = medicine.SellingPriceCentavos,
                NewSellingPriceCentavos = input.SellingPriceCentavos,
                Reason = Clean(input.PriceChangeReason),
                ChangedByUserId = userId
            });
        }

        Apply(medicine, input, barcode);
        medicine.UpdatedAt = DateTime.UtcNow;

        return await SaveAsync(db, () => id);
    }

    /// <summary>Medicines are deactivated (hidden from selling), never deleted.</summary>
    public async Task<OperationResult> SetActiveAsync(int id, bool isActive)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var medicine = await db.Medicines.SingleOrDefaultAsync(m => m.Id == id);
        if (medicine is null)
        {
            return OperationResult.Fail("This medicine no longer exists.");
        }

        medicine.IsActive = isActive;
        medicine.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return OperationResult.Ok(id);
    }

    public async Task<List<PriceHistoryEntry>> GetPriceHistoryAsync(int medicineId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.MedicinePriceHistories.AsNoTracking()
            .Where(h => h.MedicineId == medicineId)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Select(h => new PriceHistoryEntry(
                h.ChangedAt,
                h.OldPurchaseCostCentavos, h.NewPurchaseCostCentavos,
                h.OldSellingPriceCentavos, h.NewSellingPriceCentavos,
                h.Reason,
                h.ChangedByUser != null ? h.ChangedByUser.Username : null))
            .ToListAsync();
    }

    public static string? Validate(MedicineInput input)
    {
        if (string.IsNullOrWhiteSpace(input.GenericName))
            return "Generic name is required.";
        if (input.GenericName.Trim().Length > Medicine.MaxNameLength)
            return $"Generic name must be {Medicine.MaxNameLength} characters or fewer.";
        if (Clean(input.BrandName)?.Length > Medicine.MaxNameLength)
            return $"Brand name must be {Medicine.MaxNameLength} characters or fewer.";
        if (Clean(input.Barcode)?.Length > Medicine.MaxBarcodeLength)
            return $"Barcode must be {Medicine.MaxBarcodeLength} characters or fewer.";
        if (Clean(input.Strength)?.Length > Medicine.MaxStrengthLength)
            return $"Strength must be {Medicine.MaxStrengthLength} characters or fewer.";
        if (input.CategoryId <= 0)
            return "Please choose a category.";
        if (input.DosageFormId <= 0)
            return "Please choose a dosage form.";
        if (input.UnitOfMeasureId <= 0)
            return "Please choose a unit of measure.";
        if (input.PurchaseCostCentavos < 0 || input.SellingPriceCentavos < 0)
            return "Prices cannot be negative.";
        if (input.ReorderLevel < 0)
            return "Reorder level cannot be negative.";
        if (!Enum.IsDefined(input.PrescriptionClass))
            return "Please choose a prescription classification.";
        if (Clean(input.PriceChangeReason)?.Length > MedicinePriceHistory.MaxReasonLength)
            return $"Reason must be {MedicinePriceHistory.MaxReasonLength} characters or fewer.";

        return null;
    }

    private static void Apply(Medicine medicine, MedicineInput input, string? barcode)
    {
        medicine.GenericName = input.GenericName!.Trim();
        medicine.BrandName = Clean(input.BrandName);
        medicine.Barcode = barcode;
        medicine.CategoryId = input.CategoryId;
        medicine.DosageFormId = input.DosageFormId;
        medicine.Strength = Clean(input.Strength);
        medicine.UnitOfMeasureId = input.UnitOfMeasureId;
        medicine.PurchaseCostCentavos = input.PurchaseCostCentavos;
        medicine.SellingPriceCentavos = input.SellingPriceCentavos;
        medicine.ReorderLevel = input.ReorderLevel;
        medicine.PrescriptionClass = input.PrescriptionClass;
        medicine.IsVatExempt = input.IsVatExempt;
    }

    private static async Task<OperationResult> SaveAsync(AppDbContext db, Func<int> getId)
    {
        try
        {
            await db.SaveChangesAsync();
            return OperationResult.Ok(getId());
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperationResult.Fail("Another medicine already uses this barcode.");
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperationResult.Fail(
                "The selected category, dosage form or unit is no longer available. Please reselect it.");
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
