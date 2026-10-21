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
[Route("api/admin/rooms")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AdminRoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RoomWorkflow _workflow;
    private readonly AuditService _audit;
    public AdminRoomsController(AppDbContext db, RoomWorkflow workflow, AuditService audit) { _db = db; _workflow = workflow; _audit = audit; }

    /// <summary>create room</summary>
    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(UpsertRoomRequest req, CancellationToken ct)
    {
        var number = req.RoomNumber.Trim();
        if (await _db.Rooms.AnyAsync(r => r.RoomNumber == number, ct))
            throw ApiException.Conflict($"Room number {number} already exists.");

        var room = new Room
        {
            RoomNumber = number, RoomType = req.RoomType.Trim(), Floor = req.Floor,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            LastUpdatedAt = DateTime.UtcNow
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);

        _audit.Record(User.GetUserId(), "RoomCreated", nameof(Room), room.Id, $"Room {room.RoomNumber} ({room.RoomType})");
        await _db.SaveChangesAsync(ct);
        return Created($"/api/rooms/{room.Id}", RoomDto.From(room));
    }

    /// <summary>edit room</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RoomDto>> Update(int id, UpsertRoomRequest req, CancellationToken ct)
    {
        if (req.ExpectedVersion is null) throw ApiException.BadRequest("expectedVersion is required when updating a room.");
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw ApiException.NotFound("Room");
        RoomWorkflow.CheckVersion(room, req.ExpectedVersion);

        var number = req.RoomNumber.Trim();
        if (await _db.Rooms.AnyAsync(r => r.RoomNumber == number && r.Id != id, ct))
            throw ApiException.Conflict($"Room number {number} already exists.");

        var before = $"{room.RoomNumber}/{room.RoomType}/F{room.Floor}";
        room.RoomNumber = number;
        room.RoomType = req.RoomType.Trim();
        room.Floor = req.Floor;
        room.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        room.LastUpdatedAt = DateTime.UtcNow;

        _audit.Record(User.GetUserId(), "RoomUpdated", nameof(Room), room.Id, $"{before} -> {room.RoomNumber}/{room.RoomType}/F{room.Floor}");
        await _db.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }

    /// <summary>delete room</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? expectedVersion, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw ApiException.NotFound("Room");
        RoomWorkflow.CheckVersion(room, expectedVersion);

        if (room.DisplayStatus == RoomDisplayStatus.Occupied)
            throw ApiException.Conflict("An occupied room cannot be deleted.");
        if (await _db.HousekeepingNotices.AnyAsync(n => n.RoomId == id, ct) || await _db.RoomStatusHistory.AnyAsync(h => h.RoomId == id, ct))
            throw ApiException.Conflict("This room has housekeeping notices or status history and cannot be deleted. Set it Out of Service instead.");

        _db.Rooms.Remove(room);
        _audit.Record(User.GetUserId(), "RoomDeleted", nameof(Room), room.Id, $"Room {room.RoomNumber}");
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>restore room</summary>
    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<RoomDto>> Restore(int id, RestoreRoomRequest req, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw ApiException.NotFound("Room");
        RoomWorkflow.CheckVersion(room, req.ExpectedVersion);
        _workflow.Restore(room, User.GetUserId(), req.Reason, req.ConfirmUnoccupiedAndReady);
        await _db.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }
}
