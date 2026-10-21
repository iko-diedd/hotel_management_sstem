using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AdminAuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminAuditController(AppDbContext db) => _db = db;

    /// <summary>audit log, newest first</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditDto>>> List(
        [FromQuery(Name = "type")] string? actionType, [FromQuery] string? entityType, [FromQuery] int? userId,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<AuditLog> q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actionType)) { var a = actionType.Trim(); q = q.Where(x => x.Action == a); }
        if (!string.IsNullOrWhiteSpace(entityType)) { var e = entityType.Trim(); q = q.Where(x => x.EntityType == e); }
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        if (from.HasValue) q = q.Where(x => x.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(x => x.Timestamp <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditDto(x.Id, x.UserId, x.User != null ? x.User.Name : null, x.Action, x.EntityType, x.EntityId, x.Timestamp, x.Details))
            .ToListAsync(ct);
        return new PagedResult<AuditDto>(items, page, pageSize, total);
    }
}
