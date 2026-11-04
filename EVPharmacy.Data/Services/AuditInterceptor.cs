
using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace EVPharmacy.Data.Services;

/// <summary>Who is signed in. Set by the application when a user logs in or out.</summary>
public sealed class AuditContextAccessor
{
    public int? UserId { get; set; }

    public string? Username { get; set; }
}

/// <summary>
/// Writes an audit record for important changes (medicines, prices, suppliers, stock
/// adjustments, batch status, sales and voids, users, settings) every time they are saved,
/// so no screen can forget to log. Ledger rows that already are their own history
/// (sale lines, receipts' movements, price history) are not duplicated.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    /// <summary>Audit failures must never break a sale; the app can hook this to log them.</summary>
    public static Action<Exception>? ErrorHandler { get; set; }

    private sealed record Pending(string Action, string TypeLabel, EntityEntry Entry, string? Details);

    private readonly AuditContextAccessor _who;
    private readonly ConditionalWeakTable<DbContext, List<Pending>> _pending = new();

    public AuditInterceptor(AuditContextAccessor who)
    {
        _who = who;
    }

    // ----- hooks -----

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FlushAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } c) _pending.Remove(c);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } c) _pending.Remove(c);
        return Task.CompletedTask;
    }

    // ----- capture (before save, while original values still exist) -----

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            context.ChangeTracker.DetectChanges();

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified && e.Entity is not AuditLog)
                .ToList();

            var list = new List<Pending>();
            foreach (var entry in entries)
            {
                var pending = Evaluate(entry);
                if (pending is not null)
                {
                    list.Add(pending);
                }
            }

            if (list.Count > 0)
            {
                _pending.Remove(context);
                _pending.Add(context, list);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler?.Invoke(ex);
        }
    }

    private static Pending? Evaluate(EntityEntry entry)
    {
        var added = entry.State == EntityState.Added;

        switch (entry.Entity)
        {
            case Medicine:
                return ForEditable(entry, "Medicine", added);
            case Category:
                return ForEditable(entry, "Category", added);
            case DosageForm:
                return ForEditable(entry, "Dosage form", added);
            case UnitOfMeasure:
                return ForEditable(entry, "Unit of measure", added);
            case Supplier:
                return ForEditable(entry, "Supplier", added);

            case StoreSettings when !added:
                var settingsDetails = Diff(entry);
                return settingsDetails is null ? null : new Pending("Updated", "Settings", entry, settingsDetails);

            case StockReceipt when added:
                return new Pending("Created", "Stock receipt", entry, null);

            case InventoryBatch when !added && entry.Property(nameof(InventoryBatch.Status)).IsModified:
                var status = entry.Property(nameof(InventoryBatch.Status));
                return new Pending("StatusChanged", "Batch", entry,
                    $"Status: {status.OriginalValue} → {status.CurrentValue}" +
                    (entry.Entity is InventoryBatch { StatusNote: { Length: > 0 } note } ? $"\nReason: {note}" : string.Empty));

            case StockMovement { MovementType: MovementType.Adjustment } when added:
                return new Pending("Adjusted", "Stock adjustment", entry, null);

            case Sale when added:
                return new Pending("Created", "Sale", entry, null);
            case Sale when !added && entry.Property(nameof(Sale.Status)).IsModified:
                return new Pending("Voided", "Sale", entry,
                    entry.Entity is Sale { VoidReason: { Length: > 0 } reason } ? $"Reason: {reason}" : null);

            case User:
                if (added)
                {
                    return new Pending("Created", "User", entry, null);
                }

                var userDetails = Diff(entry);
                if (userDetails is null)
                {
                    return null;
                }

                var statusChanged = entry.Property(nameof(User.Status)).IsModified;
                var action = statusChanged
                    ? (entry.Entity is User { Status: UserStatus.Active } ? "Reactivated" : "Deactivated")
                    : "Updated";
                return new Pending(action, "User", entry, userDetails);

            default:
                return null;
        }
    }

    /// <summary>Medicines, suppliers and lookups: created, updated, deactivated or reactivated.</summary>
    private static Pending? ForEditable(EntityEntry entry, string label, bool added)
    {
        if (added)
        {
            return new Pending("Created", label, entry, null);
        }

        var details = Diff(entry);
        if (details is null)
        {
            return null;
        }

        var action = "Updated";
        var active = entry.Metadata.FindProperty("IsActive");
        if (active is not null && entry.Property("IsActive").IsModified)
        {
            action = (bool)entry.Property("IsActive").CurrentValue! ? "Reactivated" : "Deactivated";
        }

        return new Pending(action, label, entry, details);
    }

    /// <summary>Changed fields as "Name: old → new" lines, or null when nothing meaningful changed.</summary>
    private static string? Diff(EntityEntry entry)
    {
        var sb = new StringBuilder();

        foreach (var p in entry.Properties.Where(p => p.IsModified))
        {
            var name = p.Metadata.Name;
            if (name is "UpdatedAt" or "CreatedAt")
            {
                continue;
            }

            if (Equals(p.OriginalValue, p.CurrentValue))
            {
                continue;
            }

            // Never write password hashes anywhere.
            if (name == nameof(User.PasswordHash))
            {
                sb.AppendLine("Password: changed");
                continue;
            }

            sb.AppendLine($"{Humanize(name)}: {Show(name, p.OriginalValue)} → {Show(name, p.CurrentValue)}");
        }

        return sb.Length == 0 ? null : sb.ToString().TrimEnd();
    }

    private static string Show(string property, object? value)
    {
        return value switch
        {
            null => "(none)",
            long centavos when property.EndsWith("Centavos", StringComparison.Ordinal) => Money.Format(centavos),
            bool b => b ? "yes" : "no",
            DateTime d => d.ToLocalTime().ToString("g"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Humanize(string name)
    {
        if (name.EndsWith("Centavos", StringComparison.Ordinal))
        {
            name = name[..^"Centavos".Length];
        }

        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                sb.Append(i == 0 ? name[i] : name[i]);
            }
        }

        return sb.ToString();
    }

    // ----- write (after save, when generated ids are known) -----

    private async Task FlushAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.TryGetValue(context, out var list))
        {
            return;
        }

        _pending.Remove(context);

        try
        {
            var now = DateTime.UtcNow;

            foreach (var p in list)
            {
                var entity = p.Entry.Entity;
                var id = p.Entry.Metadata.FindPrimaryKey()?.Properties.Count == 1
                    ? p.Entry.Property(p.Entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue?.ToString()
                    : null;

                var summary = $"{p.TypeLabel} {Verb(p.Action)}: {Describe(entity)}";

                context.Set<AuditLog>().Add(new AuditLog
                {
                    OccurredAt = now,
                    UserId = _who.UserId,
                    Username = _who.Username,
                    Action = p.Action,
                    EntityType = p.Entry.Metadata.ClrType.Name,
                    EntityId = id,
                    Summary = Trim(summary, AuditLog.MaxSummaryLength),
                    Details = p.Details is null ? null : Trim(p.Details, AuditLog.MaxDetailsLength)
                });
            }

            // Re-entering SaveChanges is safe: the interceptor ignores changes made only to audit rows.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorHandler?.Invoke(ex);
        }
    }

    private static string Verb(string action) => action switch
    {
        "Created" => "created",
        "Updated" => "updated",
        "Deactivated" => "deactivated",
        "Reactivated" => "reactivated",
        "StatusChanged" => "status changed",
        "Adjusted" => "recorded",
        "Voided" => "voided",
        _ => action.ToLowerInvariant()
    };

    private static string Describe(object entity) => entity switch
    {
        Medicine m => string.IsNullOrWhiteSpace(m.BrandName) ? m.GenericName : $"{m.GenericName} ({m.BrandName})",
        LookupItem l => l.Name,
        Supplier s => s.Name,
        StockReceipt r => $"{(r.ReceiptType == ReceiptType.OpeningBalance ? "opening balance" : "delivery")}" +
                          (string.IsNullOrWhiteSpace(r.InvoiceNumber) ? string.Empty : $" {r.InvoiceNumber}"),
        InventoryBatch b => $"batch {b.BatchNumber}",
        StockMovement m => $"{(m.QuantityChange > 0 ? "+" : string.Empty)}{m.QuantityChange:N0} " +
                           $"({(m.AdjustmentReason is { } reason ? AdjustmentReasons.Label(reason) : "adjustment")})" +
                           (m.Batch is not null ? $" on batch {m.Batch.BatchNumber}" : $" on batch #{m.BatchId}"),
        Sale s => $"{s.ReceiptNumber} for {Money.Format(s.TotalCentavos)}" +
                  (s.DiscountType == DiscountType.None ? string.Empty : s.DiscountType == DiscountType.Pwd ? " (PWD)" : " (Senior Citizen)"),
        User u => u.Username,
        StoreSettings => "store settings",
        _ => entity.GetType().Name
    };

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
