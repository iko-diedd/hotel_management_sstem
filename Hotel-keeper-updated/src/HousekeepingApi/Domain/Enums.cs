namespace HousekeepingApi.Domain;

public enum UserRole { RoomAssistant, Housekeeper, Admin }

public enum OccupancyStatus { Vacant, Occupied }

public enum HousekeepingStatus { Clean, Dirty, Cleaning }

public enum ServiceStatus { InService, OutOfService }

public enum RoomDisplayStatus { Available, Occupied, Dirty, Cleaning, OutOfService }

public enum NoticeStatus { Pending, Acknowledged, Cleaning, Completed, Cancelled }

public static class Roles
{
    public const string RoomAssistant = nameof(UserRole.RoomAssistant);
    public const string Housekeeper = nameof(UserRole.Housekeeper);
    public const string Admin = nameof(UserRole.Admin);

    public const string AllStaff = RoomAssistant + "," + Housekeeper + "," + Admin;
    public const string NoticeCreators = RoomAssistant + "," + Admin;
    public const string Cleaners = Housekeeper + "," + Admin;
}
