
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

/// <summary>Values entered on the settings page. Percentages are plain percents (12 = 12%).</summary>
public sealed record StoreSettingsInput(
    string? StoreName,
    string? Address,
    string? Tin,
    string? PermitInfo,
    string? ContactNumber,
    string? ReceiptFooter,
    bool IsVatRegistered,
    decimal VatRatePercent,
    decimal SeniorDiscountPercent,
    decimal PwdDiscountPercent);

/// <summary>Business details and tax rules (a single settings row).</summary>
public class SettingsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SettingsService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<StoreSettings> GetAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var settings = await db.StoreSettingsRows.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == StoreSettings.SingletonId);

        // The migration seeds this row; this only covers a damaged database.
        return settings ?? new StoreSettings();
    }

    public async Task<OperationResult> SaveAsync(StoreSettingsInput input)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var settings = await db.StoreSettingsRows.SingleOrDefaultAsync(s => s.Id == StoreSettings.SingletonId);
        if (settings is null)
        {
            settings = new StoreSettings();
            db.StoreSettingsRows.Add(settings);
        }

        settings.StoreName = input.StoreName!.Trim();
        settings.Address = Clean(input.Address);
        settings.Tin = Clean(input.Tin);
        settings.PermitInfo = Clean(input.PermitInfo);
        settings.ContactNumber = Clean(input.ContactNumber);
        settings.ReceiptFooter = Clean(input.ReceiptFooter);
        settings.IsVatRegistered = input.IsVatRegistered;
        settings.VatRateBps = ToBps(input.VatRatePercent);
        settings.SeniorDiscountBps = ToBps(input.SeniorDiscountPercent);
        settings.PwdDiscountBps = ToBps(input.PwdDiscountPercent);
        settings.IsConfirmed = true;
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return OperationResult.Ok(settings.Id);
    }

    public static string? Validate(StoreSettingsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.StoreName))
            return "Store name is required.";
        if (input.StoreName.Trim().Length > StoreSettings.MaxNameLength)
            return $"Store name must be {StoreSettings.MaxNameLength} characters or fewer.";
        if (Clean(input.Address)?.Length > StoreSettings.MaxAddressLength)
            return $"Address must be {StoreSettings.MaxAddressLength} characters or fewer.";
        if (Clean(input.Tin)?.Length > StoreSettings.MaxTinLength)
            return $"TIN must be {StoreSettings.MaxTinLength} characters or fewer.";
        if (Clean(input.PermitInfo)?.Length > StoreSettings.MaxPermitLength)
            return $"Permit information must be {StoreSettings.MaxPermitLength} characters or fewer.";
        if (Clean(input.ContactNumber)?.Length > StoreSettings.MaxContactLength)
            return $"Contact number must be {StoreSettings.MaxContactLength} characters or fewer.";
        if (Clean(input.ReceiptFooter)?.Length > StoreSettings.MaxFooterLength)
            return $"Receipt footer must be {StoreSettings.MaxFooterLength} characters or fewer.";

        foreach (var (label, value) in new[]
                 {
                     ("VAT rate", input.VatRatePercent),
                     ("Senior Citizen discount", input.SeniorDiscountPercent),
                     ("PWD discount", input.PwdDiscountPercent)
                 })
        {
            if (value < 0 || value > 100)
                return $"{label} must be between 0 and 100 percent.";
            if (decimal.Round(value, 2) != value)
                return $"{label} can have at most two decimal places.";
        }

        return null;
    }

    private static int ToBps(decimal percent) => (int)Math.Round(percent * 100m, 0, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
