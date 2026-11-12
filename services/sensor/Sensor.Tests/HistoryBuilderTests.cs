using Sensor.Api.Services;

namespace Sensor.Tests;

public class HistoryBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 25, 0, DateTimeKind.Utc);

    [Fact]
    public void Hourly_AlwaysReturnsOnePointPerHour_LastIsCurrentHour()
    {
        var points = HistoryBuilder.Hourly([], Now, 24);

        Assert.Equal(24, points.Count);
        Assert.Equal(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc), points[^1].HourStartUtc);
        Assert.Equal(new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc), points[0].HourStartUtc);
        Assert.All(points, p => Assert.Null(p.Average));
    }

    [Fact]
    public void Hourly_AveragesMinMaxPerHour()
    {
        var samples = new[]
        {
            (new DateTime(2026, 11, 10, 9, 5, 0, DateTimeKind.Utc), 40),
            (new DateTime(2026, 11, 10, 9, 20, 0, DateTimeKind.Utc), 50),
            (new DateTime(2026, 11, 10, 8, 59, 0, DateTimeKind.Utc), 30),
        };

        var points = HistoryBuilder.Hourly(samples, Now, 24);

        Assert.Equal(45, points[^1].Average);
        Assert.Equal(40, points[^1].Min);
        Assert.Equal(50, points[^1].Max);
        Assert.Equal(30, points[^2].Average);
    }

    [Fact]
    public void Hourly_IgnoresSamplesOutsideTheWindow()
    {
        var samples = new[]
        {
            (Now.AddHours(-30), 10),
            (Now.AddHours(1), 90),
        };

        var points = HistoryBuilder.Hourly(samples, Now, 24);

        Assert.All(points, p => Assert.Null(p.Average));
    }

    [Fact]
    public void FirstHour_IsStartOfOldestBucket() =>
        Assert.Equal(new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc), HistoryBuilder.FirstHour(Now, 24));
}
