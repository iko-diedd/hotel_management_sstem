using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Infrastructure;

namespace HousekeepingApi.Services;

public class RoomWorkflow
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public RoomWorkflow(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    public static void CheckVersion(IVersioned entity, int? expected)
    {
        if (expected.HasValue && expected.Value != entity.Version)
            throw ApiException.Conflict("The record was modified by someone else (version mismatch). Reload it and try again.");
    }

    public void ChangeStatus(Room room, RoomDisplayStatus target, int userId, UserRole role, string? reason, bool confirmReady)
    {
        var previous = room.DisplayStatus;
        var result = RoomRules.Apply(room, target, role, reason, confirmReady);
        Throw(result);
        Record(room, previous, userId, reason, "RoomStatusChanged");
    }

    public void Restore(Room room, int userId, string? reason, bool confirmReady)
    {
        var previous = room.DisplayStatus;
        Throw(RoomRules.Restore(room, confirmReady));
        Record(room, previous, userId, reason ?? "Restored to service", "RoomRestored");
    }

    private void Record(Room room, RoomDisplayStatus previous, int userId, string? reason, string action)
    {
        var now = DateTime.UtcNow;
        room.LastUpdatedAt = now;
        var next = room.DisplayStatus;
        _db.RoomStatusHistory.Add(new RoomStatusHistory
        {
            Room = room, RoomId = room.Id, PreviousStatus = previous, NewStatus = next,
            ChangedById = userId, ChangedAt = now,
            Reason = reason is { Length: > 500 } ? reason[..500] : reason
        });
        _audit.Record(userId, action, nameof(Room), room.Id, $"{previous} -> {next}");
    }

    private static void Throw(RoomRules.Result r)
    {
        if (!r.Ok) throw new ApiException(r.StatusCode, r.Error!);
    }
}
