using Microsoft.EntityFrameworkCore;
using Sensor.Api.Data;

namespace Sensor.Api.Services;

// Runs in the background once an hour and deletes readings and pump runs older than
// Sensor:RetentionDays, so sensor_db does not grow forever.
public class ReadingCleanupService(
    IServiceScopeFactory scopes,
    SensorSettings settings,
    ILogger<ReadingCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await CleanUpAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed cleanup must never stop the service; try again next hour
                logger.LogWarning("Sensor data cleanup failed: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanUpAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);

        // ExecuteDeleteAsync runs one DELETE statement in the database (no rows loaded into memory)
        var readings = await db.Readings.Where(r => r.RecordedAtUtc < cutoff).ExecuteDeleteAsync(ct);
        var runs = await db.PumpRuns.Where(p => p.StartedAtUtc < cutoff).ExecuteDeleteAsync(ct);

        if (readings + runs > 0)
            logger.LogInformation("Deleted {Readings} readings and {Runs} pump runs older than {Days} days", readings, runs, settings.RetentionDays);
    }
}
