using HousekeepingApi.Domain;
using Xunit;
using static HousekeepingApi.Domain.RoomDisplayStatus;
using static HousekeepingApi.Domain.UserRole;

namespace HousekeepingApi.Tests;

public class RoomRulesTests
{
    private static Room In(RoomDisplayStatus s) => s switch
    {
        Available => new Room { OccupancyStatus = OccupancyStatus.Vacant, HousekeepingStatus = HousekeepingStatus.Clean },
        RoomDisplayStatus.Occupied => new Room { OccupancyStatus = OccupancyStatus.Occupied, HousekeepingStatus = HousekeepingStatus.Clean },
        Dirty => new Room { OccupancyStatus = OccupancyStatus.Vacant, HousekeepingStatus = HousekeepingStatus.Dirty },
        Cleaning => new Room { OccupancyStatus = OccupancyStatus.Vacant, HousekeepingStatus = HousekeepingStatus.Cleaning },
        _ => new Room { ServiceStatus = ServiceStatus.OutOfService }
    };

    // current, target, role, expected HTTP status
    [Theory]
    // allowed
    [InlineData(Available, RoomDisplayStatus.Occupied, RoomAssistant, 200)]
    [InlineData(Available, RoomDisplayStatus.Occupied, Admin, 200)]
    [InlineData(RoomDisplayStatus.Occupied, Dirty, RoomAssistant, 200)]
    [InlineData(Available, Dirty, RoomAssistant, 200)]
    [InlineData(Dirty, Cleaning, Housekeeper, 200)]
    [InlineData(Dirty, Cleaning, Admin, 200)]
    [InlineData(Cleaning, Dirty, Housekeeper, 200)]
    [InlineData(Available, OutOfService, RoomAssistant, 200)]
    // forbidden by role
    [InlineData(Available, RoomDisplayStatus.Occupied, Housekeeper, 403)]
    [InlineData(RoomDisplayStatus.Occupied, Dirty, Housekeeper, 403)]
    [InlineData(Dirty, Cleaning, RoomAssistant, 403)]
    [InlineData(Cleaning, Available, Housekeeper, 403)]
    [InlineData(Available, OutOfService, Housekeeper, 403)]
    // wrong current state
    [InlineData(Available, Cleaning, Admin, 409)]
    [InlineData(Dirty, Available, Admin, 409)]
    [InlineData(Dirty, RoomDisplayStatus.Occupied, RoomAssistant, 409)]
    [InlineData(Available, Available, Admin, 409)]
    [InlineData(OutOfService, Available, Admin, 409)]
    [InlineData(OutOfService, Dirty, Admin, 409)]
    public void Apply_enforces_role_and_state_rules(RoomDisplayStatus current, RoomDisplayStatus target, UserRole role, int expected)
    {
        var room = In(current);
        var result = RoomRules.Apply(room, target, role, reason: "broken AC", confirmReady: true);

        Assert.Equal(expected, result.StatusCode);
        Assert.Equal(expected == 200, result.Ok);
        if (expected == 200) Assert.Equal(target, RoomRules.Display(room));
        else Assert.Equal(current, RoomRules.Display(room)); // a rejected change must not alter the room
    }

    [Fact]
    public void Available_requires_explicit_confirmation()
    {
        var room = In(Cleaning);
        var result = RoomRules.Apply(room, Available, RoomAssistant, null, confirmReady: false);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(Cleaning, RoomRules.Display(room));

        Assert.True(RoomRules.Apply(room, Available, RoomAssistant, null, confirmReady: true).Ok);
        Assert.Equal(Available, RoomRules.Display(room));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OutOfService_requires_a_reason(string? reason)
    {
        var room = In(Available);
        var result = RoomRules.Apply(room, OutOfService, Admin, reason, false);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(Available, RoomRules.Display(room));
    }

    [Fact]
    public void Restore_defaults_to_dirty_and_can_be_marked_clean()
    {
        var dirty = In(OutOfService);
        Assert.True(RoomRules.Restore(dirty, confirmReady: false).Ok);
        Assert.Equal(Dirty, RoomRules.Display(dirty));

        var clean = In(OutOfService);
        Assert.True(RoomRules.Restore(clean, confirmReady: true).Ok);
        Assert.Equal(Available, RoomRules.Display(clean));
    }

    [Fact]
    public void Restore_rejects_a_room_that_is_in_service()
        => Assert.Equal(409, RoomRules.Restore(In(Available), true).StatusCode);

    [Fact]
    public void Out_of_service_wins_over_every_other_state()
    {
        var room = new Room { ServiceStatus = ServiceStatus.OutOfService, OccupancyStatus = OccupancyStatus.Occupied, HousekeepingStatus = HousekeepingStatus.Dirty };
        Assert.Equal(OutOfService, RoomRules.Display(room));
    }

    // The SQL filter (Matches) must agree with the in-memory rule (Display) for every combination.
    [Fact]
    public void Matches_expression_agrees_with_Display_for_all_combinations()
    {
        foreach (var svc in Enum.GetValues<ServiceStatus>())
        foreach (var occ in Enum.GetValues<OccupancyStatus>())
        foreach (var hk in Enum.GetValues<HousekeepingStatus>())
        {
            var room = new Room { ServiceStatus = svc, OccupancyStatus = occ, HousekeepingStatus = hk };
            foreach (var status in Enum.GetValues<RoomDisplayStatus>())
                Assert.Equal(RoomRules.Display(room) == status, RoomRules.Matches(status).Compile()(room));
        }
    }
}
