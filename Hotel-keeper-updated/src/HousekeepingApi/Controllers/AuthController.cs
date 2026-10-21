using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using HousekeepingApi.Infrastructure;
using HousekeepingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens, AuditService audit)
    { _db = db; _hasher = hasher; _tokens = tokens; _audit = audit; }

    /// <summary>user login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var username = req.Username.Trim();
        // Lock out per username AND client IP, so one attacker cannot lock the real user out.
        var key = $"{username}|{HttpContext.Connection.RemoteIpAddress}";
        var since = DateTime.UtcNow - FailureWindow;
        var failures = await _db.AuditLogs.CountAsync(a => a.Action == "LoginFailed" && a.Details == key && a.Timestamp > since, ct);
        if (failures >= MaxFailures)
            throw new ApiException(429, "Too many failed sign-in attempts. Try again in a few minutes.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        var verified = false;
        if (user is not null)
        {
            var r = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            verified = r != PasswordVerificationResult.Failed;
            if (r == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = _hasher.HashPassword(user, req.Password);
        }
        else
        {
            // equalise timing
            _hasher.HashPassword(new User(), req.Password);
        }

        if (user is null || !verified || !user.IsActive)
        {
            _audit.Record(user?.Id, "LoginFailed", nameof(User), user?.Id, key);
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Unauthorized", Detail = "Invalid username or password." });
        }

        _audit.Record(user.Id, "LoginSucceeded", nameof(User), user.Id);
        await _db.SaveChangesAsync(ct);

        var (token, expires) = _tokens.Create(user);
        return Ok(new LoginResponse(token, "Bearer", expires, ToDto(user)));
    }

    /// <summary>current user</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == User.GetUserId(), ct);
        return ToDto(user);
    }

    /// <summary>change password</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == User.GetUserId(), ct);
        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw ApiException.BadRequest("Current password is incorrect.");
        var problem = PasswordPolicy.Validate(req.NewPassword);
        if (problem is not null) throw ApiException.BadRequest(problem);

        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        _audit.Record(user.Id, "PasswordChanged", nameof(User), user.Id);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    internal static UserDto ToDto(User u) => new(u.Id, u.Name, u.Username, u.Role, u.IsActive, u.CreatedAt);
}
