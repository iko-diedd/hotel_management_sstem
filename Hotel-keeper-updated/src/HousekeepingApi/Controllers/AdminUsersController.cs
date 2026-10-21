using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using HousekeepingApi.Infrastructure;
using HousekeepingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly AuditService _audit;
    public AdminUsersController(AppDbContext db, IPasswordHasher<User> hasher, AuditService audit) { _db = db; _hasher = hasher; _audit = audit; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> List([FromQuery] UserRole? role, [FromQuery] bool? isActive, CancellationToken ct)
    {
        IQueryable<User> q = _db.Users.AsNoTracking();
        if (role.HasValue) q = q.Where(u => u.Role == role.Value);
        if (isActive.HasValue) q = q.Where(u => u.IsActive == isActive.Value);
        var users = await q.OrderBy(u => u.Name).ToListAsync(ct);
        return users.Select(AuthController.ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("User");
        return AuthController.ToDto(u);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req, CancellationToken ct)
    {
        var problem = PasswordPolicy.Validate(req.Password);
        if (problem is not null) throw ApiException.BadRequest(problem);

        var username = req.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            throw ApiException.Conflict($"Username '{username}' is already taken.");

        var user = new User { Name = req.Name.Trim(), Username = username, Role = req.Role!.Value, IsActive = true, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _audit.Record(User.GetUserId(), "UserCreated", nameof(Domain.User), user.Id, $"{user.Username} as {user.Role}");
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/users/{user.Id}", AuthController.ToDto(user));
    }

    /// <summary>update user</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw ApiException.NotFound("User");
        var newRole = req.Role!.Value;
        var newActive = req.IsActive!.Value;
        var losesAdmin = user.Role == UserRole.Admin && user.IsActive && (newRole != UserRole.Admin || !newActive);

        if (losesAdmin)
        {
            if (user.Id == User.GetUserId())
                throw ApiException.BadRequest("You cannot demote or deactivate your own account.");
            if (!await _db.Users.AnyAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, ct))
                throw ApiException.Conflict("At least one active administrator must remain.");
        }

        var before = $"{user.Role}/{(user.IsActive ? "active" : "inactive")}";
        user.Name = req.Name.Trim();
        user.Role = newRole;
        user.IsActive = newActive;

        _audit.Record(User.GetUserId(), "UserUpdated", nameof(Domain.User), user.Id, $"{before} -> {user.Role}/{(user.IsActive ? "active" : "inactive")}");
        await _db.SaveChangesAsync(ct);
        return AuthController.ToDto(user);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest req, CancellationToken ct)
    {
        var problem = PasswordPolicy.Validate(req.NewPassword);
        if (problem is not null) throw ApiException.BadRequest(problem);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw ApiException.NotFound("User");

        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        _audit.Record(User.GetUserId(), "PasswordReset", nameof(Domain.User), user.Id);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
