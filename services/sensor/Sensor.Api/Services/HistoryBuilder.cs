using Sensor.Api.DTOs;

namespace Sensor.Api.Services;

// Turns raw samples into one point per hour for the 24-hour chart.
// The last bucket is the current (unfinished) hour; hours without data get null values.
public static class HistoryBuilder
{
    public static List<HistoryPointDto> Hourly(IEnumerable<(DateTime AtUtc, int Moisture)> samples, DateTime nowUtc, int hours)
    {
        var firstHour = FirstHour(nowUtc, hours);

        var buckets = new List<int>[hours];
        foreach (var (at, moisture) in samples)
        {
            var index = (int)Math.Floor((at - firstHour).TotalHours);
            if (index < 0 || index >= hours) continue;     // outside the window
            (buckets[index] ??= []).Add(moisture);
        }

        var points = new List<HistoryPointDto>(hours);
        for (var i = 0; i < hours; i++)
        {
            var b = buckets[i];
            points.Add(b is null
                ? new HistoryPointDto(firstHour.AddHours(i), null, null, null)
                : new HistoryPointDto(firstHour.AddHours(i), (int)Math.Round(b.Average()), b.Min(), b.Max()));
        }
        return points;
    }

    // Start of the oldest bucket: the current hour minus (hours - 1)
    public static DateTime FirstHour(DateTime nowUtc, int hours)
    {
        var currentHour = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);
        return currentHour.AddHours(-(hours - 1));
    }
}
