
namespace EVPharmacy.Core.Entities;

/// <summary>
/// Base for simple admin-maintained lookup lists (categories, dosage forms, units).
/// Items are deactivated rather than deleted so historical records keep their meaning.
/// </summary>
public abstract class LookupItem
{
    public const int MaxNameLength = 60;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
