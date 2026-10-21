using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HousekeepingApi.Domain;

public interface IVersioned
{
    /// <summary>concurrency token</summary>
    int Version { get; set; }
}

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(50)] public string Username { get; set; } = "";
    [Required, MaxLength(255)] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Room : IVersioned
{
    public int Id { get; set; }
    [Required, MaxLength(10)] public string RoomNumber { get; set; } = "";
    [Required, MaxLength(50)] public string RoomType { get; set; } = "";
    public int Floor { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public OccupancyStatus OccupancyStatus { get; set; } = OccupancyStatus.Vacant;
    public HousekeepingStatus HousekeepingStatus { get; set; } = HousekeepingStatus.Clean;
    public ServiceStatus ServiceStatus { get; set; } = ServiceStatus.InService;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; }

    [NotMapped]
    public RoomDisplayStatus DisplayStatus => RoomRules.Display(this);
}

public class HousekeepingNotice : IVersioned
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    [Required, MaxLength(1000)] public string Message { get; set; } = "";
    public NoticeStatus Status { get; set; } = NoticeStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Version { get; set; }
}

public class RoomStatusHistory
{
    public long Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public RoomDisplayStatus PreviousStatus { get; set; }
    public RoomDisplayStatus NewStatus { get; set; }
    public int ChangedById { get; set; }
    public User ChangedBy { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(500)] public string? Reason { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = "";
    [Required, MaxLength(50)] public string EntityType { get; set; } = "";
    [MaxLength(50)] public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    [MaxLength(500)] public string? Details { get; set; }
}
