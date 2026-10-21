using HousekeepingApi.Data;
using HousekeepingApi.Domain;

namespace HousekeepingApi.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public void Record(int? userId, string action, string entityType, object? entityId = null, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 500 } ? details[..500] : details,
            Timestamp = DateTime.UtcNow
        });
    }
}
