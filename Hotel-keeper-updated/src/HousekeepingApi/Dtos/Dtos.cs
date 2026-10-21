using System.ComponentModel.DataAnnotations;
using HousekeepingApi.Domain;

namespace HousekeepingApi.Dtos;

public record LoginRequest([Required, StringLength(50)] string Username, [Required, StringLength(128)] string Password);
public record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, UserDto User);
public record ChangePasswordRequest([Required] string CurrentPassword, [Required, StringLength(128)] string NewPassword);

public record UserDto(int Id, string Name, string Username, UserRole Role, bool IsActive, DateTime CreatedAt);

public class CreateUserRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    [Required, RegularExpression(@"^[A-Za-z0-9._-]{3,50}$", ErrorMessage = "Username must be 3-50 characters: letters, digits, '.', '_' or '-'.")]
    public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
    [Required, EnumDataType(typeof(UserRole))] public UserRole? Role { get; set; }
}

public class UpdateUserRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    [Required, EnumDataType(typeof(UserRole))] public UserRole? Role { get; set; }
    [Required] public bool? IsActive { get; set; }
}

public class ResetPasswordRequest
{
    [Required, StringLength(128)] public string NewPassword { get; set; } = "";
}

public record PublicRoomDto(string RoomNumber, string RoomType, int Floor, string? Description, bool IsAvailable);
public record RoomTypeAvailability(string RoomType, int TotalRooms, int AvailableRooms);
public record PublicAvailabilityDto(int TotalRooms, int AvailableRooms, IReadOnlyList<RoomTypeAvailability> ByRoomType, IReadOnlyList<PublicRoomDto> AvailableList);

public record RoomDto(
    int Id, string RoomNumber, string RoomType, int Floor, string? Description,
    RoomDisplayStatus Status, OccupancyStatus OccupancyStatus, HousekeepingStatus HousekeepingStatus, ServiceStatus ServiceStatus,
    bool IsAvailable, DateTime LastUpdatedAt, int Version)
{
    public static RoomDto From(Room r) => new(
        r.Id, r.RoomNumber, r.RoomType, r.Floor, r.Description,
        r.DisplayStatus, r.OccupancyStatus, r.HousekeepingStatus, r.ServiceStatus,
        r.DisplayStatus == RoomDisplayStatus.Available, r.LastUpdatedAt, r.Version);
}

public class UpsertRoomRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9-]{1,10}$", ErrorMessage = "Room number must be 1-10 letters, digits or '-'.")]
    public string RoomNumber { get; set; } = "";
    [Required, StringLength(50, MinimumLength = 1)] public string RoomType { get; set; } = "";
    [Range(0, 200)] public int Floor { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    /// <summary>expected version</summary>
    public int? ExpectedVersion { get; set; }
}

public class ChangeRoomStatusRequest
{
    [Required, EnumDataType(typeof(RoomDisplayStatus))] public RoomDisplayStatus? Status { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    /// <summary>confirm ready</summary>
    public bool ConfirmUnoccupiedAndReady { get; set; }
    public int? ExpectedVersion { get; set; }
}

public class RestoreRoomRequest
{
    [StringLength(500)] public string? Reason { get; set; }
    public bool ConfirmUnoccupiedAndReady { get; set; }
    public int? ExpectedVersion { get; set; }
}

public record RoomHistoryDto(long Id, int RoomId, string RoomNumber, RoomDisplayStatus PreviousStatus, RoomDisplayStatus NewStatus, int ChangedById, string ChangedBy, DateTime ChangedAt, string? Reason);

public record NoticeDto(
    int Id, int RoomId, string RoomNumber, string Message, NoticeStatus Status,
    int CreatedById, string CreatedBy, int? AssignedToId, string? AssignedTo,
    DateTime CreatedAt, DateTime? AcknowledgedAt, DateTime? CompletedAt, int Version);

public class CreateNoticeRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9-]{1,10}$")] public string RoomNumber { get; set; } = "";
    [Required, StringLength(1000, MinimumLength = 1)] public string Message { get; set; } = "";
    public int? AssignedToId { get; set; }
}

public class UpdateNoticeStatusRequest
{
    [Required, EnumDataType(typeof(NoticeStatus))] public NoticeStatus? Status { get; set; }
    public int? ExpectedVersion { get; set; }
}

public class AssignNoticeRequest
{
    public int? AssignedToId { get; set; }
    public int? ExpectedVersion { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record DashboardDto(
    int TotalRooms, Dictionary<string, int> RoomsByStatus, int AvailableRooms, double OccupancyPercent,
    Dictionary<string, int> NoticesByStatus, int OpenNotices, int NoticesCompletedToday,
    DateTime? OldestOpenNoticeAt, double? AverageCompletionMinutesLast7Days,
    int OverdueNotices, int OverdueAfterMinutes, string TimeZone);

public record AuditDto(long Id, int? UserId, string? UserName, string Action, string EntityType, string? EntityId, DateTime Timestamp, string? Details);
