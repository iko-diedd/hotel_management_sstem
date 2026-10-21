using System.Linq.Expressions;

namespace HousekeepingApi.Domain;

public static class RoomRules
{
    public static RoomDisplayStatus Display(Room r)
    {
        if (r.ServiceStatus == ServiceStatus.OutOfService) return RoomDisplayStatus.OutOfService;
        if (r.OccupancyStatus == OccupancyStatus.Occupied) return RoomDisplayStatus.Occupied;
        return r.HousekeepingStatus switch
        {
            HousekeepingStatus.Dirty => RoomDisplayStatus.Dirty,
            HousekeepingStatus.Cleaning => RoomDisplayStatus.Cleaning,
            _ => RoomDisplayStatus.Available
        };
    }

    public static Expression<Func<Room, bool>> Matches(RoomDisplayStatus s) => s switch
    {
        RoomDisplayStatus.OutOfService => r => r.ServiceStatus == ServiceStatus.OutOfService,
        RoomDisplayStatus.Occupied => r => r.ServiceStatus == ServiceStatus.InService && r.OccupancyStatus == OccupancyStatus.Occupied,
        RoomDisplayStatus.Dirty => r => r.ServiceStatus == ServiceStatus.InService && r.OccupancyStatus == OccupancyStatus.Vacant && r.HousekeepingStatus == HousekeepingStatus.Dirty,
        RoomDisplayStatus.Cleaning => r => r.ServiceStatus == ServiceStatus.InService && r.OccupancyStatus == OccupancyStatus.Vacant && r.HousekeepingStatus == HousekeepingStatus.Cleaning,
        _ => r => r.ServiceStatus == ServiceStatus.InService && r.OccupancyStatus == OccupancyStatus.Vacant && r.HousekeepingStatus == HousekeepingStatus.Clean
    };

    public sealed record Result(bool Ok, int StatusCode, string? Error)
    {
        public static Result Success() => new(true, 200, null);
        public static Result Forbidden(string m) => new(false, 403, m);
        public static Result Conflict(string m) => new(false, 409, m);
        public static Result Invalid(string m) => new(false, 400, m);
    }

    public static Result Apply(Room room, RoomDisplayStatus target, UserRole role, string? reason, bool confirmReady)
    {
        var current = Display(room);

        if (current == RoomDisplayStatus.OutOfService)
            return Result.Conflict("Room is Out of Service. An administrator must restore it to service first.");
        if (current == target)
            return Result.Conflict($"Room is already {target}.");

        switch (target)
        {
            case RoomDisplayStatus.Dirty:
                if (current != RoomDisplayStatus.Cleaning && role == UserRole.Housekeeper)
                    return Result.Forbidden("Housekeepers may only set Dirty on a room that is being cleaned.");
                room.OccupancyStatus = OccupancyStatus.Vacant;
                room.HousekeepingStatus = HousekeepingStatus.Dirty;
                return Result.Success();

            case RoomDisplayStatus.Cleaning:
                if (role == UserRole.RoomAssistant)
                    return Result.Forbidden("Only housekeepers and administrators can start cleaning.");
                if (current != RoomDisplayStatus.Dirty)
                    return Result.Conflict("Only a Dirty room can move to Cleaning.");
                room.HousekeepingStatus = HousekeepingStatus.Cleaning;
                return Result.Success();

            case RoomDisplayStatus.Available:
                if (role == UserRole.Housekeeper)
                    return Result.Forbidden("Housekeepers cannot mark a room Available; a room assistant or administrator must confirm it.");
                if (current != RoomDisplayStatus.Cleaning)
                    return Result.Conflict("A room can only become Available after cleaning (current status must be Cleaning).");
                if (!confirmReady)
                    return Result.Invalid("confirmUnoccupiedAndReady must be true: confirm the room is unoccupied and ready for use.");
                room.OccupancyStatus = OccupancyStatus.Vacant;
                room.HousekeepingStatus = HousekeepingStatus.Clean;
                return Result.Success();

            case RoomDisplayStatus.Occupied:
                if (role == UserRole.Housekeeper)
                    return Result.Forbidden("Housekeepers cannot mark a room Occupied.");
                if (current != RoomDisplayStatus.Available)
                    return Result.Conflict("Only an Available room can be marked Occupied.");
                room.OccupancyStatus = OccupancyStatus.Occupied;
                return Result.Success();

            case RoomDisplayStatus.OutOfService:
                if (role == UserRole.Housekeeper)
                    return Result.Forbidden("Housekeepers cannot take a room out of service.");
                if (string.IsNullOrWhiteSpace(reason))
                    return Result.Invalid("A reason is required to take a room Out of Service.");
                room.ServiceStatus = ServiceStatus.OutOfService;
                return Result.Success();

            default:
                return Result.Invalid("Unsupported target status.");
        }
    }

    public static Result Restore(Room room, bool confirmReady)
    {
        if (room.ServiceStatus != ServiceStatus.OutOfService)
            return Result.Conflict("Room is not Out of Service.");
        room.ServiceStatus = ServiceStatus.InService;
        room.OccupancyStatus = OccupancyStatus.Vacant;
        room.HousekeepingStatus = confirmReady ? HousekeepingStatus.Clean : HousekeepingStatus.Dirty;
        return Result.Success();
    }
}
