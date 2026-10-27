namespace Identity.Api.Models;

// 3 vai tro trong he thong
public static class Roles
{
    public const string User = "User";
    public const string Expert = "Expert";
    public const string Admin = "Admin";

    public static readonly string[] All = [User, Expert, Admin];
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";

    // Chi luu ban da bam bang BCrypt, khong bao gio luu mat khau goc
    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = Roles.User;
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
