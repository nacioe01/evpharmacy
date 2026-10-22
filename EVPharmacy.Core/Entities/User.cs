
namespace EVPharmacy.Core.Entities;

public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    // Store the password hash, never the plaintext password.
    public required string Password { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "active";

    public bool IsAdmin { get; set; } = false;
}
