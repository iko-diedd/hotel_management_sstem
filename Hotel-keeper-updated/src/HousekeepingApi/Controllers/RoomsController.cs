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
[Route("api/rooms")]
[Authorize(Roles = Roles.AllStaff)]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RoomWorkflow _workflow;
    public RoomsController(AppDbContext db, RoomWorkflow workflow) { _db = db; _workflow = workflow; }

    /// <summary>list rooms</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> List(
        [FromQuery] RoomDisplayStatus? status, [FromQuery] string? roomType, [FromQuery] int? floor, [FromQuery] string? search,
        CancellationToken ct)
    {
        IQueryable<Room> q = _db.Rooms.AsNoTracking();
        if (status.HasValue) q = q.Where(RoomRules.Matches(status.Value));
        if (!string.IsNullOrWhiteSpace(roomType)) q = q.Where(r => r.RoomType == roomType);
        if (floor.HasValue) q = q.Where(r => r.Floor == floor.Value);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(r => r.RoomNumber.StartsWith(search.Trim()));
        var rooms = await q.OrderBy(r => r.RoomNumber).ToListAsync(ct);
        return rooms.Select(RoomDto.From).ToList();
    }

    /// <summary>availability counts</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<Dictionary<string, int>>> Availability(CancellationToken ct)
    {
        var result = new Dictionary<string, int>();
        foreach (var s in Enum.GetValues<RoomDisplayStatus>())
            result[s.ToString()] = await _db.Rooms.CountAsync(RoomRules.Matches(s), ct);
        return result;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> Get(int id, CancellationToken ct)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw ApiException.NotFound("Room");
        return RoomDto.From(room);
    }

    /// <summary>status history</summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<IEnumerable<RoomHistoryDto>>> History(int id, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        if (!await _db.Rooms.AnyAsync(r => r.Id == id, ct)) throw ApiException.NotFound("Room");
        limit = Math.Clamp(limit, 1, 500);
        return await _db.RoomStatusHistory.AsNoTracking()
            .Where(h => h.RoomId == id)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Take(limit)
            .Select(h => new RoomHistoryDto(h.Id, h.RoomId, h.Room.RoomNumber, h.PreviousStatus, h.NewStatus, h.ChangedById, h.ChangedBy.Name, h.ChangedAt, h.Reason))
            .ToListAsync(ct);
    }

    /// <summary>change status</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<RoomDto>> ChangeStatus(int id, ChangeRoomStatusRequest req, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw ApiException.NotFound("Room");
        RoomWorkflow.CheckVersion(room, req.ExpectedVersion);

        _workflow.ChangeStatus(room, req.Status!.Value, User.GetUserId(), User.GetRole(), req.Reason, req.ConfirmUnoccupiedAndReady);
        await _db.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }
}
