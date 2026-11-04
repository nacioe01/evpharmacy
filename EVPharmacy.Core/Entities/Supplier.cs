
namespace EVPharmacy.Core.Entities;

public class Supplier
{
    public const int MaxNameLength = 120;
    public const int MaxContactLength = 120;
    public const int MaxPhoneLength = 40;
    public const int MaxEmailLength = 120;
    public const int MaxAddressLength = 250;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
