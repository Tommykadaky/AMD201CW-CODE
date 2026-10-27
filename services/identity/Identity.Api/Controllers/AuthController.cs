using System.Security.Claims;
using Identity.Api.Data;
using Identity.Api.DTOs;
using Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, IdentityDbContext db) : ControllerBase
{
    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        try { return Ok(await auth.RegisterAsync(req)); }
        catch (AuthException ex) { return StatusCode(ex.StatusCode, new ErrorResponse(ex.Message)); }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        try { return Ok(await auth.LoginAsync(req)); }
        catch (AuthException ex) { return StatusCode(ex.StatusCode, new ErrorResponse(ex.Message)); }
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        try { return Ok(await auth.RefreshAsync(req.RefreshToken)); }
        catch (AuthException ex) { return StatusCode(ex.StatusCode, new ErrorResponse(ex.Message)); }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        await auth.LogoutAsync(req.RefreshToken);
        return NoContent();
    }

    // GET /api/auth/me  (phai gui kem token)
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        return user is null ? NotFound() : Ok(AuthService.ToDto(user));
    }
}
