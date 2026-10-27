using System.Security.Claims;
using Identity.Api.Data;
using Identity.Api.DTOs;
using Identity.Api.Models;
using Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Controllers;

// Chi tai khoan co role Admin moi goi duoc cac API nay
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(IdentityDbContext db) : ControllerBase
{
    // GET /api/admin/users
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await db.Users.OrderBy(u => u.CreatedAt).ToListAsync();
        return Ok(users.Select(AuthService.ToDto));
    }

    // PUT /api/admin/users/{id}/role
    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserDto>> ChangeRole(Guid id, ChangeRoleRequest req)
    {
        if (!Roles.All.Contains(req.Role))
            return BadRequest(new ErrorResponse("Vai trò không hợp lệ. Chọn User, Expert hoặc Admin."));

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (currentId == id.ToString())
            return BadRequest(new ErrorResponse("Không thể tự đổi vai trò của chính mình."));

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound(new ErrorResponse("Không tìm thấy người dùng."));

        user.Role = req.Role;
        await db.SaveChangesAsync();
        return Ok(AuthService.ToDto(user));
    }
}
