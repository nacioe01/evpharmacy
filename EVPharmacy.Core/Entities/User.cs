
namespace EVPharmacy.Core.Entities;

public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    // PBKDF2 hash produced by PasswordHasher. Never the plaintext password.
    public required string PasswordHash { get; set; }

    // Always stored and read back as UTC.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public bool IsAdmin { get; set; } = false;
}
