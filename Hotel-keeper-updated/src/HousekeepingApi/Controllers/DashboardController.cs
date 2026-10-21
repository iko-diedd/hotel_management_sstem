using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.AllStaff)]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private static readonly NoticeStatus[] OpenStatuses = { NoticeStatus.Pending, NoticeStatus.Acknowledged, NoticeStatus.Cleaning };

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public DashboardController(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    /// <summary>dashboard stats</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
    {
        // one grouped query instead of one count per status
        var groups = await _db.Rooms.AsNoTracking()
            .GroupBy(r => new { r.ServiceStatus, r.OccupancyStatus, r.HousekeepingStatus })
            .Select(g => new { g.Key.ServiceStatus, g.Key.OccupancyStatus, g.Key.HousekeepingStatus, Count = g.Count() })
            .ToListAsync(ct);
        var roomsByStatus = Enum.GetValues<RoomDisplayStatus>().ToDictionary(s => s.ToString(), _ => 0);
        foreach (var g in groups)
        {
            var shown = RoomRules.Display(new Room
            {
                ServiceStatus = g.ServiceStatus, OccupancyStatus = g.OccupancyStatus, HousekeepingStatus = g.HousekeepingStatus
            });
            roomsByStatus[shown.ToString()] += g.Count;
        }
        var total = roomsByStatus.Values.Sum();
        var inService = total - roomsByStatus[nameof(RoomDisplayStatus.OutOfService)];
        var occupancy = inService == 0 ? 0 : Math.Round(100.0 * roomsByStatus[nameof(RoomDisplayStatus.Occupied)] / inService, 1);

        var noticeCounts = await _db.HousekeepingNotices.AsNoTracking()
            .GroupBy(n => n.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var noticesByStatus = Enum.GetValues<NoticeStatus>().ToDictionary(s => s.ToString(), s => noticeCounts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);
        var open = noticesByStatus[nameof(NoticeStatus.Pending)] + noticesByStatus[nameof(NoticeStatus.Acknowledged)] + noticesByStatus[nameof(NoticeStatus.Cleaning)];

        // "today" is the hotel's local day, not the UTC day
        var zone = Zone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified), zone);
        var completedToday = await _db.HousekeepingNotices.CountAsync(n => n.Status == NoticeStatus.Completed && n.CompletedAt >= todayStartUtc, ct);

        var oldest = await _db.HousekeepingNotices.Where(n => OpenStatuses.Contains(n.Status))
            .OrderBy(n => n.CreatedAt).Select(n => (DateTime?)n.CreatedAt).FirstOrDefaultAsync(ct);

        var overdueMinutes = Math.Max(1, _config.GetValue("Hotel:OverdueNoticeMinutes", 30));
        var cutoff = DateTime.UtcNow.AddMinutes(-overdueMinutes);
        var overdue = await _db.HousekeepingNotices.CountAsync(n => OpenStatuses.Contains(n.Status) && n.CreatedAt < cutoff, ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var durations = await _db.HousekeepingNotices.AsNoTracking()
            .Where(n => n.Status == NoticeStatus.Completed && n.CompletedAt >= weekAgo)
            .Select(n => new { n.CreatedAt, n.CompletedAt }).ToListAsync(ct);
        double? avg = durations.Count == 0 ? null : Math.Round(durations.Average(d => (d.CompletedAt!.Value - d.CreatedAt).TotalMinutes), 1);

        return new DashboardDto(total, roomsByStatus, roomsByStatus[nameof(RoomDisplayStatus.Available)], occupancy,
            noticesByStatus, open, completedToday, oldest, avg, overdue, overdueMinutes, zone.Id);
    }

    private TimeZoneInfo Zone()
    {
        var id = _config["Hotel:TimeZone"];
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
