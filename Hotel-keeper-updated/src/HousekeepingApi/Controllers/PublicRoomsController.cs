using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Route("api/public/rooms")]
[AllowAnonymous]
[EnableRateLimiting("public")]
[Produces("application/json")]
public class PublicRoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicRoomsController(AppDbContext db) => _db = db;

    private static readonly System.Linq.Expressions.Expression<Func<Room, bool>> IsAvailable = RoomRules.Matches(RoomDisplayStatus.Available);

    /// <summary>list rooms</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublicRoomDto>>> List([FromQuery] string? roomType, [FromQuery] bool availableOnly = false, CancellationToken ct = default)
    {
        IQueryable<Room> q = _db.Rooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(roomType)) q = q.Where(r => r.RoomType == roomType);
        if (availableOnly) q = q.Where(IsAvailable);

        var rooms = await q.OrderBy(r => r.RoomNumber).ToListAsync(ct);
        return rooms.Select(ToPublic).ToList();
    }

    /// <summary>availability summary</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<PublicAvailabilityDto>> Availability([FromQuery] string? roomType, CancellationToken ct = default)
    {
        IQueryable<Room> q = _db.Rooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(roomType)) q = q.Where(r => r.RoomType == roomType);
        var rooms = await q.OrderBy(r => r.RoomNumber).ToListAsync(ct);

        var byType = rooms.GroupBy(r => r.RoomType)
            .Select(g => new RoomTypeAvailability(g.Key, g.Count(), g.Count(r => r.DisplayStatus == RoomDisplayStatus.Available)))
            .OrderBy(t => t.RoomType).ToList();
        var available = rooms.Where(r => r.DisplayStatus == RoomDisplayStatus.Available).Select(ToPublic).ToList();
        return new PublicAvailabilityDto(rooms.Count, available.Count, byType, available);
    }

    /// <summary>room details</summary>
    [HttpGet("{roomNumber}")]
    public async Task<ActionResult<PublicRoomDto>> Get(string roomNumber, CancellationToken ct)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomNumber == roomNumber, ct);
        return room is null ? NotFound(new ProblemDetails { Status = 404, Title = "Not found", Detail = "Room was not found." }) : ToPublic(room);
    }

    private static PublicRoomDto ToPublic(Room r) =>
        new(r.RoomNumber, r.RoomType, r.Floor, r.Description, r.DisplayStatus == RoomDisplayStatus.Available);
}
