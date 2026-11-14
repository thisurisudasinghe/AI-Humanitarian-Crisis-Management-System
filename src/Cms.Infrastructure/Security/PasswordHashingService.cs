
using Cms.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Cms.Infrastructure.Security;

public class PasswordHashingService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "Password cannot be empty.", nameof(password));
        }

        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(
        User user,
        string hashedPassword,
        string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) ||
            string.IsNullOrEmpty(providedPassword))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            hashedPassword,
            providedPassword);

        return result != PasswordVerificationResult.Failed;
    }
}
