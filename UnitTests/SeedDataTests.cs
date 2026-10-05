using DbConnection;
using DbConnection.Domain.Entities;
using FluentAssertions;

namespace UnitTests;

public class SeedDataTests
{
    [Test]
    public void MeetingRoomSeedDataCanBeLoadedFromTestOutput()
    {
        var seedFile = Path.Combine(AppContext.BaseDirectory, "InitialSeedData", "MeetingRoom.json");
        File.Exists(seedFile).Should().BeTrue();
        
        var rooms = new SeedInitialDataFromJson<List<MeetingRoom>>().SeedData("MeetingRoom");

        rooms.Should().NotBeNullOrEmpty();
        rooms.All(room => room.Id != Guid.Empty && !string.IsNullOrWhiteSpace(room.Name)).Should().BeTrue();
    }
}
