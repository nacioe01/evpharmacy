
namespace EVPharmacy.Core.Entities;

/// <summary>
/// One line of the audit trail: who did what, and when. Append-only; rows are never
/// edited or deleted. The username is copied so the record stays readable forever.
/// </summary>
public class AuditLog
{
    public const int MaxActionLength = 30;
    public const int MaxEntityTypeLength = 40;
    public const int MaxEntityIdLength = 40;
    public const int MaxSummaryLength = 500;
    public const int MaxDetailsLength = 2000;

    public int Id { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string? Username { get; set; }

    /// <summary>Created, Updated, Login, LoginFailed, Logout, Backup, Restore, and so on.</summary>
    public required string Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public required string Summary { get; set; }

    /// <summary>Field-by-field changes, one per line, when relevant.</summary>
    public string? Details { get; set; }
}
