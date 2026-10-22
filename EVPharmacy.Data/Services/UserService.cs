
using Microsoft.EntityFrameworkCore;
using EVPharmacy.Core.Entities;
using EVPharmacy.Core.Security;
using EVPharmacy.Data.Database;

namespace EVPharmacy.Data.Services;

public class UserService
{
    private readonly AppDbContext _dbContext;

    public UserService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> CreateAdminAsync(
        string username,
        string password)
    {
        var normalizedUsername = username.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUsername) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "Username and password are required.");
        }

        var exists = await _dbContext.Users.AnyAsync(
            user => user.Username == normalizedUsername);

        if (exists)
        {
            return false;
        }

        var admin = new User
        {
            Username = normalizedUsername,
            Password = PasswordHasher.HashPassword(password),
            Status = "active",
            IsAdmin = true
        };

        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public enum AuthenticationResult
    {
        Success,
        InvalidCredentials,
        AccountDeactivated
    }


    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return AuthenticationResult.InvalidCredentials;
        }

        var normalizedUsername = username.Trim();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                user => user.Username == normalizedUsername);

        if (user is null ||
            !PasswordHasher.VerifyPassword(password, user.Password))
        {
            return AuthenticationResult.InvalidCredentials;
        }

        if (user.Status != "active")
        {
            return AuthenticationResult.AccountDeactivated;
        }

        return AuthenticationResult.Success;
    }

}
