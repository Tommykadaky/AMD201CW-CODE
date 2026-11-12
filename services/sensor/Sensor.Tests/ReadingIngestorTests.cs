using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sensor.Api.Data;
using Sensor.Api.DTOs;
using Sensor.Api.Messaging;
using Sensor.Api.Models;
using Sensor.Api.Services;
using TreeO.Contracts.Events;

namespace Sensor.Tests;

// Flow tests: the real ReadingIngestor against an in-memory database and a fake RabbitMQ publisher.
// Each Send() uses a fresh DbContext, like one HTTP request from the device.
// Time-based rules (offline, tank check delay, run length) are tested by moving stored timestamps back.
public class ReadingIngestorTests
{
    private const string DeviceId = "TREEO_TEST_01";
    private const string Key = "test-key-0123456789abcdef";
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SensorSettings _settings = new();
    private readonly FakePublisher _publisher = new();

    // ----- Registration and keys -----

    [Fact]
    public async Task UnknownDevice_IsRegistered_AndWrongKeyIsRejected()
    {
        var reply = await Send(40, pumpOn: false);

        Assert.NotNull(reply);
        Assert.Equal(0, reply.PumpSeconds);
        Assert.Equal(30, reply.DryThreshold);
        Assert.Equal(60, reply.WetThreshold);
        Assert.Null(await Send(40, pumpOn: false, key: "wrong-key-0123456789"));
    }

    [Fact]
    public async Task UnpairedDevice_NeverSendsNotifications()
    {
        await Send(25, pumpOn: false);
        await Send(25, pumpOn: true);
        await Send(60, pumpOn: false);

        Assert.Empty(_publisher.Sent);
    }

    // ----- "Water now" -----

    [Fact]
    public async Task WaterNow_FullFlow_RecordsManualRunAndNotifies()
    {
        await PairedDevice(40);
        await QueueWater(5);

        var reply = await Send(40, pumpOn: false);            // device picks up the command
        Assert.Equal(5, reply!.PumpSeconds);
        Assert.Null((await GetDevice()).PendingPumpSeconds);

        await Send(40, pumpOn: true);                          // pump started
        var run = Assert.Single(await Runs());
        Assert.Equal(PumpTriggers.Manual, run.Trigger);
        Assert.Null(run.EndedAtUtc);
        Assert.Null((await GetDevice()).CommandSentAtUtc);    // used up by this run

        await Send(62, pumpOn: false);                         // pump stopped
        run = Assert.Single(await Runs());
        Assert.NotNull(run.EndedAtUtc);
        Assert.Equal(62, run.MoistureAfter);
        var message = Assert.Single(_publisher.Sent);
        Assert.Equal(NotificationTypes.Pump, message.Type);
        Assert.Equal(run.Id, (await GetDevice()).TankCheckRunId);
    }

    [Fact]
    public async Task WaterNow_WhilePumpIsRunning_IsKeptUntilThePumpStops()
    {
        await PairedDevice(25);
        await Send(25, pumpOn: true);                          // automatic run in progress
        await QueueWater(5);

        var reply = await Send(40, pumpOn: true);
        Assert.Equal(0, reply!.PumpSeconds);
        Assert.Equal(5, (await GetDevice()).PendingPumpSeconds);

        reply = await Send(60, pumpOn: false);                 // run finished -> command delivered now
        Assert.Equal(5, reply!.PumpSeconds);
        Assert.Null((await GetDevice()).PendingPumpSeconds);
    }

    [Fact]
    public async Task WaterNow_WhenSoilIsAtHardStop_IsDropped()
    {
        await PairedDevice(90);
        await QueueWater(5);

        var reply = await Send(90, pumpOn: false);

        Assert.Equal(0, reply!.PumpSeconds);
        Assert.Null((await GetDevice()).PendingPumpSeconds);
    }

    [Fact]
    public async Task WaterNow_ExpiredCommand_IsNotDelivered()
    {
        await PairedDevice(40);
        await Change(d => { d.PendingPumpSeconds = 5; d.PendingPumpAtUtc = DateTime.UtcNow.AddMinutes(-5); });

        var reply = await Send(40, pumpOn: false);

        Assert.Equal(0, reply!.PumpSeconds);
        Assert.Null((await GetDevice()).PendingPumpSeconds);
    }

    // ----- Offline -----

    [Fact]
    public async Task DeviceOfflineDuringPumpRun_RunIsClosedWithoutNotification()
    {
        await PairedDevice(25);
        await Send(25, pumpOn: true);
        await Change(d => d.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-10));   // then silence for 10 minutes

        await Send(50, pumpOn: false);                         // back online

        var run = Assert.Single(await Runs());
        Assert.NotNull(run.EndedAtUtc);
        Assert.Empty(_publisher.Sent);                         // unknown real duration: no "watered for 600s"
        Assert.False((await GetDevice()).PumpOn);
    }

    [Fact]
    public async Task DeviceOfflineDuringPumpRun_ThenPumpingAgain_OpensANewRun()
    {
        await PairedDevice(25);
        await Send(25, pumpOn: true);
        await Change(d => d.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-10));

        await Send(25, pumpOn: true);                          // a new run started while it was offline

        var runs = await Runs();
        Assert.Equal(2, runs.Count);
        Assert.Single(runs, r => r.EndedAtUtc is null);
    }

    // ----- Empty water tank -----

    [Fact]
    public async Task EmptyTank_IsReportedOnce_ThenClearedWhenWaterArrives()
    {
        await PairedDevice(25);

        // Run 1: pump runs 8 s, the soil stays dry
        await AutoRun(before: 25, after: 25);
        Assert.Single(_publisher.Sent, m => m.Type == NotificationTypes.Pump);
        await MakeTankCheckDue();
        await Send(25, pumpOn: false);
        Assert.Single(_publisher.Sent, m => m.Type == NotificationTypes.WaterTank);
        Assert.True((await GetDevice()).TankEmpty);

        // Run 2: still empty -> no fake "Plant watered", no second tank alert
        await AutoRun(before: 25, after: 25);
        await MakeTankCheckDue();
        await Send(25, pumpOn: false);
        Assert.Single(_publisher.Sent, m => m.Type == NotificationTypes.Pump);
        Assert.Single(_publisher.Sent, m => m.Type == NotificationTypes.WaterTank);

        // Run 3: tank refilled, the soil gets wet
        await AutoRun(before: 25, after: 60);
        Assert.False((await GetDevice()).TankEmpty);
        Assert.Equal(2, _publisher.Sent.Count(m => m.Type == NotificationTypes.Pump));
    }

    [Fact]
    public async Task TankCheck_IsNotDoneBeforeItIsDue()
    {
        await PairedDevice(25);
        await AutoRun(before: 25, after: 25);

        await Send(25, pumpOn: false);                         // 45 s have not passed yet

        Assert.DoesNotContain(_publisher.Sent, m => m.Type == NotificationTypes.WaterTank);
        Assert.NotNull((await GetDevice()).TankCheckRunId);
    }

    // ----- Dry soil -----

    [Fact]
    public async Task DrySoil_WithAutoWateringOff_AlertsOnceInTheCooldown()
    {
        await PairedDevice(40);
        await Change(d => d.AutoWatering = false);

        await Send(20, pumpOn: false);
        await Send(19, pumpOn: false);

        Assert.Single(_publisher.Sent, m => m.Type == NotificationTypes.SoilDry);
    }

    // ---------- helpers ----------

    private SensorDbContext NewDb() =>
        new(new DbContextOptionsBuilder<SensorDbContext>().UseInMemoryDatabase(_dbName).Options);

    private async Task<ReadingResponse?> Send(int moisture, bool pumpOn, string key = Key)
    {
        await using var db = NewDb();
        var ingestor = new ReadingIngestor(db, _publisher, _settings, NullLogger<ReadingIngestor>.Instance);
        return await ingestor.IngestAsync(new ReadingRequest(DeviceId, moisture, pumpOn, -50, "test"), key, "127.0.0.1", CancellationToken.None);
    }

    // First reading registers the device, then a user pairs it
    private async Task PairedDevice(int moisture)
    {
        await Send(moisture, pumpOn: false);
        await Change(d => d.OwnerUserId = Owner);
    }

    private Task QueueWater(int seconds) =>
        Change(d => { d.PendingPumpSeconds = seconds; d.PendingPumpAtUtc = DateTime.UtcNow; });

    // An automatic run that lasted 8 s
    private async Task AutoRun(int before, int after)
    {
        await Send(before, pumpOn: true);
        await using (var db = NewDb())
        {
            var open = await db.PumpRuns.SingleAsync(r => r.EndedAtUtc == null);
            open.StartedAtUtc = open.StartedAtUtc.AddSeconds(-8);
            await db.SaveChangesAsync();
        }
        await Send(after, pumpOn: false);
    }

    private Task MakeTankCheckDue() => Change(d => d.TankCheckDueAtUtc = DateTime.UtcNow.AddSeconds(-1));

    private async Task Change(Action<Device> edit)
    {
        await using var db = NewDb();
        edit(await db.Devices.SingleAsync());
        await db.SaveChangesAsync();
    }

    private async Task<Device> GetDevice()
    {
        await using var db = NewDb();
        return await db.Devices.AsNoTracking().SingleAsync();
    }

    private async Task<List<PumpRun>> Runs()
    {
        await using var db = NewDb();
        return await db.PumpRuns.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
    }

    private sealed class FakePublisher : INotificationPublisher
    {
        public List<NotificationRequested> Sent { get; } = [];

        public Task<bool> PublishAsync(NotificationRequested message, CancellationToken ct = default)
        {
            lock (Sent) Sent.Add(message);
            return Task.FromResult(true);
        }
    }
}
