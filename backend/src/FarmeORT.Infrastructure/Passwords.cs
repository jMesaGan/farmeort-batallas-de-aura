using FarmeORT.Application;
using FarmeORT.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
namespace FarmeORT.Infrastructure;

public class Passwords : IPasswords
{
    private readonly PasswordHasher<User> hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
    public string Hash(User user, string password) => hasher.HashPassword(user, password);
    public bool Verify(User user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
