
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

public sealed record AuditItem(
    int Id,
    DateTime OccurredAtUtc,
    string? Username,
    string Action,
    string? EntityType,
    string Summary,
    string? Details);

/// <summary>Reads the audit trail and records events that are not simple data changes (logins, backups).</summary>
public class AuditService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly AuditContextAccessor _who;

    public AuditService(IDbContextFactory<AppDbContext> contextFactory, AuditContextAccessor who)
    {
        _contextFactory = contextFactory;
        _who = who;
    }

    /// <summary>Records an event. Uses the signed-in user unless one is given. Never throws.</summary>
    public async Task LogEventAsync(
        string action,
        string summary,
        string? details = null,
        int? userId = null,
        string? username = null)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync();

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId ?? _who.UserId,
                Username = username ?? _who.Username,
                Action = action,
                Summary = Cut(summary, AuditLog.MaxSummaryLength)!,
                Details = Cut(details, AuditLog.MaxDetailsLength)
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            AuditInterceptor.ErrorHandler?.Invoke(ex);
        }
    }

    public async Task<List<AuditItem>> SearchAsync(
        DateTime? fromLocal,
        DateTime? toLocalExclusive,
        string? search,
        int take = 500)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (fromLocal is DateTime from)
        {
            var fromUtc = from.ToUniversalTime();
            query = query.Where(a => a.OccurredAt >= fromUtc);
        }

        if (toLocalExclusive is DateTime to)
        {
            var toUtc = to.ToUniversalTime();
            query = query.Where(a => a.OccurredAt < toUtc);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(a =>
                EF.Functions.Like(a.Summary, pattern, "\\") ||
                (a.Username != null && EF.Functions.Like(a.Username, pattern, "\\")) ||
                EF.Functions.Like(a.Action, pattern, "\\") ||
                (a.EntityType != null && EF.Functions.Like(a.EntityType, pattern, "\\")));
        }

        return await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new AuditItem(a.Id, a.OccurredAt, a.Username, a.Action, a.EntityType, a.Summary, a.Details))
            .ToListAsync();
    }

    private static string? Cut(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : text[..(max - 1)] + "…";
}
