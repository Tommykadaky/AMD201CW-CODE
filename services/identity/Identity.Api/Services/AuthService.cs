using Identity.Api.Data;
using Identity.Api.DTOs;
using Identity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Services;

// Loi nghiep vu co thong bao than thien de tra ve cho app
public class AuthException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class AuthService(IdentityDbContext db, JwtTokenService jwt)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email))
            throw new AuthException("Email này đã được dùng. Hãy đăng nhập hoặc dùng email khác.", 409);

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = Roles.User,   // Dang ky cong khai luon la User, Admin chi do he thong tao
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Cung mot thong bao cho ca 2 truong hop de khong lo email nao da ton tai
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new AuthException("Email hoặc mật khẩu chưa đúng.", 401);

        return await IssueTokensAsync(user);
    }

    // Dung refresh token cu de lay cap token moi (token cu bi thu hoi)
    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (stored is null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new AuthException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.", 401);

        stored.RevokedAt = DateTime.UtcNow;
        return await IssueTokensAsync(stored.User);
    }

    // Dang xuat: thu hoi refresh token, khong lam moi duoc nua
    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (stored is not null && stored.RevokedAt == null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = jwt.CreateAccessToken(user);
        var refresh = jwt.CreateRefreshToken(user.Id);

        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refresh.Token, expiresAt, ToDto(user));
    }

    public static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);
}
