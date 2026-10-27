using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Trips;

namespace Dhathuru.TripEngine.Tests;

public class EngineContractTests
{
    [Fact]
    public void Passenger_total_counts_everyone() => Assert.Equal(4, new Passengers(2, 1, 1).Total);

    [Fact]
    public void Planner_seam_is_the_engine_entry_point() =>
        Assert.True(typeof(ITripPlanner).IsInterface);
}
