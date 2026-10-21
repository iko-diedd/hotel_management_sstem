using System.Linq.Expressions;
using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using HousekeepingApi.Infrastructure;
using HousekeepingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Authorize(Roles = Roles.AllStaff)]
[Produces("application/json")]
public class NoticesController : ControllerBase
{
    private static readonly NoticeStatus[] OpenStatuses = { NoticeStatus.Pending, NoticeStatus.Acknowledged, NoticeStatus.Cleaning };

    private static readonly Expression<Func<HousekeepingNotice, NoticeDto>> Project = n => new NoticeDto(
        n.Id, n.RoomId, n.Room.RoomNumber, n.Message, n.Status,
        n.CreatedById, n.CreatedBy.Name, n.AssignedToId, n.AssignedTo != null ? n.AssignedTo.Name : null,
        n.CreatedAt, n.AcknowledgedAt, n.CompletedAt, n.Version);

    private readonly AppDbContext _db;
    private readonly RoomWorkflow _workflow;
    private readonly AuditService _audit;

    public NoticesController(AppDbContext db, RoomWorkflow workflow, AuditService audit)
    { _db = db; _workflow = workflow; _audit = audit; }

    /// <summary>list notices</summary>
    [HttpGet("api/notices")]
    public async Task<ActionResult<PagedResult<NoticeDto>>> List(
        [FromQuery] NoticeStatus? status, [FromQuery] int? roomId, [FromQuery] string? roomNumber, [FromQuery] int? createdById,
        [FromQuery] bool assignedToMe = false, [FromQuery] bool unassigned = false,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<HousekeepingNotice> q = _db.HousekeepingNotices.AsNoTracking();
        if (status.HasValue) q = q.Where(n => n.Status == status.Value);
        if (roomId.HasValue) q = q.Where(n => n.RoomId == roomId.Value);
        if (!string.IsNullOrWhiteSpace(roomNumber)) q = q.Where(n => n.Room.RoomNumber == roomNumber);
        if (createdById.HasValue) q = q.Where(n => n.CreatedById == createdById.Value);
        if (assignedToMe) { var me = User.GetUserId(); q = q.Where(n => n.AssignedToId == me); }
        if (unassigned) q = q.Where(n => n.AssignedToId == null);
        if (from.HasValue) q = q.Where(n => n.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(n => n.CreatedAt <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Project).ToListAsync(ct);
        return new PagedResult<NoticeDto>(items, page, pageSize, total);
    }

    [HttpGet("api/notices/{id:int}")]
    public async Task<ActionResult<NoticeDto>> Get(int id, CancellationToken ct) =>
        await _db.HousekeepingNotices.AsNoTracking().Where(n => n.Id == id).Select(Project).FirstOrDefaultAsync(ct)
        ?? throw ApiException.NotFound("Notice");

    /// <summary>room notices</summary>
    [HttpGet("api/rooms/{roomId:int}/notices")]
    public async Task<ActionResult<IEnumerable<NoticeDto>>> ForRoom(int roomId, CancellationToken ct)
    {
        if (!await _db.Rooms.AnyAsync(r => r.Id == roomId, ct)) throw ApiException.NotFound("Room");
        return await _db.HousekeepingNotices.AsNoTracking().Where(n => n.RoomId == roomId)
            .OrderByDescending(n => n.CreatedAt).Take(200).Select(Project).ToListAsync(ct);
    }

    /// <summary>create notice</summary>
    [HttpPost("api/notices")]
    [Authorize(Roles = Roles.NoticeCreators)]
    public async Task<ActionResult<NoticeDto>> Create(CreateNoticeRequest req, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == req.RoomNumber, ct)
                   ?? throw ApiException.NotFound($"Room {req.RoomNumber}");

        if (await _db.HousekeepingNotices.AnyAsync(n => n.RoomId == room.Id && OpenStatuses.Contains(n.Status), ct))
            throw ApiException.Conflict($"Room {room.RoomNumber} already has an open housekeeping notice.");

        if (req.AssignedToId.HasValue) await EnsureAssignable(req.AssignedToId.Value, ct);

        var notice = new HousekeepingNotice
        {
            Room = room, RoomId = room.Id, CreatedById = uid, AssignedToId = req.AssignedToId,
            Message = req.Message.Trim(), Status = NoticeStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        _db.HousekeepingNotices.Add(notice);

        // mark dirty
        var current = room.DisplayStatus;
        if (current is RoomDisplayStatus.Occupied or RoomDisplayStatus.Available)
            _workflow.ChangeStatus(room, RoomDisplayStatus.Dirty, uid, User.GetRole(), $"Key returned - notice for room {room.RoomNumber}", false);

        await _db.SaveChangesAsync(ct);
        _audit.Record(uid, "NoticeCreated", nameof(HousekeepingNotice), notice.Id, $"Room {room.RoomNumber}");
        await _db.SaveChangesAsync(ct);

        var dto = await _db.HousekeepingNotices.AsNoTracking().Where(n => n.Id == notice.Id).Select(Project).FirstAsync(ct);
        return Created($"/api/notices/{dto.Id}", dto);
    }

    /// <summary>update notice</summary>
    [HttpPatch("api/notices/{id:int}/status")]
    public async Task<ActionResult<NoticeDto>> UpdateStatus(int id, UpdateNoticeStatusRequest req, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var role = User.GetRole();
        var notice = await _db.HousekeepingNotices.Include(n => n.Room).FirstOrDefaultAsync(n => n.Id == id, ct)
                     ?? throw ApiException.NotFound("Notice");
        RoomWorkflow.CheckVersion(notice, req.ExpectedVersion);

        var target = req.Status!.Value;
        var from = notice.Status;
        var now = DateTime.UtcNow;

        switch (target)
        {
            case NoticeStatus.Acknowledged:
                RequireCleaner(role);
                RequireCurrent(notice, NoticeStatus.Pending);
                ClaimOrCheckAssignee(notice, uid, role);
                notice.Status = NoticeStatus.Acknowledged;
                notice.AcknowledgedAt = now;
                break;

            case NoticeStatus.Cleaning:
                RequireCleaner(role);
                RequireCurrent(notice, NoticeStatus.Acknowledged);
                ClaimOrCheckAssignee(notice, uid, role);
                var roomState = notice.Room.DisplayStatus;
                if (roomState == RoomDisplayStatus.Dirty)
                    _workflow.ChangeStatus(notice.Room, RoomDisplayStatus.Cleaning, uid, role, $"Cleaning started (notice {notice.Id})", false);
                else if (roomState is not (RoomDisplayStatus.Cleaning or RoomDisplayStatus.OutOfService))
                    throw ApiException.Conflict($"Room {notice.Room.RoomNumber} is {roomState}, not awaiting cleaning.");
                notice.Status = NoticeStatus.Cleaning;
                break;

            case NoticeStatus.Completed:
                RequireCleaner(role);
                RequireCurrent(notice, NoticeStatus.Cleaning);
                ClaimOrCheckAssignee(notice, uid, role);
                notice.Status = NoticeStatus.Completed;
                notice.CompletedAt = now;
                // keep status
                break;

            case NoticeStatus.Cancelled:
                if (role == UserRole.Housekeeper) throw ApiException.Forbidden("Housekeepers cannot cancel notices.");
                if (notice.Status is not (NoticeStatus.Pending or NoticeStatus.Acknowledged))
                    throw ApiException.Conflict($"A {notice.Status} notice cannot be cancelled.");
                notice.Status = NoticeStatus.Cancelled;
                break;

            default:
                throw ApiException.BadRequest("Status must be Acknowledged, Cleaning, Completed or Cancelled.");
        }

        _audit.Record(uid, "NoticeStatusChanged", nameof(HousekeepingNotice), notice.Id, $"{from} -> {notice.Status}");
        await _db.SaveChangesAsync(ct);

        return await _db.HousekeepingNotices.AsNoTracking().Where(n => n.Id == id).Select(Project).FirstAsync(ct);
    }

    /// <summary>assign notice</summary>
    [HttpPatch("api/notices/{id:int}/assign")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<NoticeDto>> Assign(int id, AssignNoticeRequest req, CancellationToken ct)
    {
        var notice = await _db.HousekeepingNotices.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiException.NotFound("Notice");
        RoomWorkflow.CheckVersion(notice, req.ExpectedVersion);
        if (notice.Status is NoticeStatus.Completed or NoticeStatus.Cancelled)
            throw ApiException.Conflict($"A {notice.Status} notice cannot be reassigned.");
        if (req.AssignedToId.HasValue) await EnsureAssignable(req.AssignedToId.Value, ct);

        notice.AssignedToId = req.AssignedToId;
        _audit.Record(User.GetUserId(), "NoticeAssigned", nameof(HousekeepingNotice), notice.Id, $"AssignedTo={req.AssignedToId?.ToString() ?? "none"}");
        await _db.SaveChangesAsync(ct);
        return await _db.HousekeepingNotices.AsNoTracking().Where(n => n.Id == id).Select(Project).FirstAsync(ct);
    }

    private async Task EnsureAssignable(int userId, CancellationToken ct)
    {
        var ok = await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role == UserRole.Housekeeper, ct);
        if (!ok) throw ApiException.BadRequest("AssignedToId must be an active housekeeper.");
    }

    private static void RequireCleaner(UserRole role)
    {
        if (role == UserRole.RoomAssistant)
            throw ApiException.Forbidden("Only housekeepers and administrators can update cleaning progress.");
    }

    private static void RequireCurrent(HousekeepingNotice n, NoticeStatus expected)
    {
        if (n.Status != expected)
            throw ApiException.Conflict($"Notice is {n.Status}; this step requires it to be {expected}.");
    }

    private static void ClaimOrCheckAssignee(HousekeepingNotice n, int uid, UserRole role)
    {
        if (n.AssignedToId is null) n.AssignedToId = uid;
        else if (n.AssignedToId != uid && role != UserRole.Admin)
            throw ApiException.Forbidden("This notice is assigned to another housekeeper.");
    }
}
