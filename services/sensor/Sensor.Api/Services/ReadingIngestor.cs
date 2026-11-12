using System.Globalization;
using TreeO.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Sensor.Api.Data;
using Sensor.Api.DTOs;
using Sensor.Api.Messaging;
using Sensor.Api.Models;

namespace Sensor.Api.Services;

// Handles one reading from a device:
//   1. check the device key (or register a new device)
//   2. clean up after an offline period (a pump run that never reported its end)
//   3. run the delayed "empty water tank" check
//   4. store a sample if ReadingPolicy says so, open / close a PumpRun when the pump switches
//   5. hand over a pending "Water now" command when the device can act on it
//   6. queue notifications (pump finished, soil dry, tank empty)
public class ReadingIngestor(
    SensorDbContext db,
    INotificationPublisher publisher,
    SensorSettings settings,
    ILogger<ReadingIngestor> logger)
{
    // Returns null when the device key is wrong (the controller answers 401).
    public async Task<ReadingResponse?> IngestAsync(ReadingRequest req, string? deviceKey, string? ipAddress, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == req.DeviceId, ct);
        if (device is null)
        {
            if (!settings.AllowAutoRegister || deviceKey is null || deviceKey.Length < DeviceKeys.MinLength) return null;

            device = new Device { Id = req.DeviceId, KeyHash = DeviceKeys.Hash(deviceKey), Name = req.DeviceId, CreatedAtUtc = now };
            db.Devices.Add(device);
            logger.LogInformation("Registered new device {DeviceId}", req.DeviceId);
        }
        else if (!DeviceKeys.Matches(deviceKey, device.KeyHash))
        {
            return null;
        }

        // ----- 1. Back after an offline period: the last reported state is stale -----
        // Example: the board lost power while pumping. Its "pump on" was the last thing we heard,
        // so the run is still open and device.PumpOn is still true. Close the run at the last time we
        // heard from the device (no notification: we do not know how long it really ran) and treat
        // the pump as off, so a new run is opened correctly if the device reports pumpOn again.
        var wasOffline = ReadingPolicy.WasOffline(device.LastSeenAtUtc, now, settings);
        if (wasOffline)
        {
            await CloseOpenRunsAsync(device.Id, device.LastSeenAtUtc!.Value, ct);
            ClearTankCheck(device);
        }

        var previousPumpOn = device.PumpOn && !wasOffline;
        var previousMoisture = device.Moisture;

        // ----- 2. Delayed tank check: did the last pump run actually wet the soil? -----
        // Done before handling a new pump start in this same reading, so the check of the previous run
        // still uses a moisture value that is not affected by the new run.
        (PumpRun Run, int MoistureNow)? tankAlert = null;
        if (device.TankCheckRunId is long checkRunId && ReadingPolicy.IsTankCheckDue(device.TankCheckDueAtUtc, now))
        {
            ClearTankCheck(device);
            var run = await db.PumpRuns.FirstOrDefaultAsync(p => p.Id == checkRunId, ct);
            if (run is { EndedAtUtc: DateTime ended } && ended - run.StartedAtUtc >= ReadingPolicy.MinRunForTankCheck)
            {
                var empty = ReadingPolicy.LooksLikeEmptyTank(run.MoistureBefore, req.Moisture, ended - run.StartedAtUtc);
                // Alert once per empty-tank episode, not after every automatic run
                if (empty && !device.TankEmpty) tankAlert = (run, req.Moisture);
                device.TankEmpty = empty;
            }
        }

        // ----- 3. Store a sample only when it adds information -----
        if (ReadingPolicy.ShouldStore(device.LastStoredAtUtc, device.LastStoredMoisture, previousPumpOn, req.Moisture, req.PumpOn, now, settings))
        {
            db.Readings.Add(new SensorReading { DeviceId = device.Id, Moisture = req.Moisture, PumpOn = req.PumpOn, RecordedAtUtc = now });
            device.LastStoredAtUtc = now;
            device.LastStoredMoisture = req.Moisture;
        }

        // ----- 4. Pump switched on / off -----
        PumpRun? finishedRun = null;
        if (!previousPumpOn && req.PumpOn)
        {
            await CloseOpenRunsAsync(device.Id, now, ct);      // never leave two open runs for one device
            ClearTankCheck(device);                            // new water is coming: the old check means nothing now

            var trigger = ReadingPolicy.ClassifyTrigger(device.CommandSentAtUtc, now);
            if (trigger == PumpTriggers.Manual) device.CommandSentAtUtc = null;   // one command = one manual run

            db.PumpRuns.Add(new PumpRun
            {
                DeviceId = device.Id,
                Trigger = trigger,
                StartedAtUtc = now,
                MoistureBefore = previousMoisture ?? req.Moisture,
            });
        }
        else if (previousPumpOn && !req.PumpOn)
        {
            finishedRun = await db.PumpRuns
                .Where(p => p.DeviceId == device.Id && p.EndedAtUtc == null)
                .OrderByDescending(p => p.StartedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (finishedRun is not null)
            {
                finishedRun.EndedAtUtc = now;
                finishedRun.MoistureAfter = req.Moisture;
                // Moisture already rose during the run: the tank has water again (e.g. it was just refilled)
                if (ReadingPolicy.SoilGotWetter(finishedRun.MoistureBefore, finishedRun.MoistureAfter)) device.TankEmpty = false;
                device.TankCheckRunId = finishedRun.Id;
                device.TankCheckDueAtUtc = now.AddSeconds(settings.TankCheckDelaySeconds);
            }
        }

        // ----- 5. Live values -----
        device.LastSeenAtUtc = now;
        device.Moisture = req.Moisture;
        device.PumpOn = req.PumpOn;
        device.IpAddress = ipAddress;
        device.WifiRssi = req.WifiRssi;
        if (!string.IsNullOrWhiteSpace(req.Firmware)) device.Firmware = req.Firmware;

        // ----- 6. Pending manual command -----
        var pumpSeconds = 0;
        var command = ReadingPolicy.ActiveCommand(device.PendingPumpSeconds, device.PendingPumpAtUtc, now, settings);
        if (command is int seconds && ReadingPolicy.CanDeliverCommand(req.PumpOn, req.Moisture, settings))
        {
            pumpSeconds = seconds;
            device.CommandSentAtUtc = now;
            ClearCommand(device);
        }
        else if (command is null || ReadingPolicy.IsTooWetToWater(req.Moisture, settings))
        {
            ClearCommand(device);    // expired, or the soil is too wet: the device would refuse it
        }
        // otherwise the pump is running right now: keep the command until the pump stops

        // ----- 7. Soil dry while auto-watering is off -----
        var sendDryAlert = device.OwnerUserId is not null
            && ReadingPolicy.ShouldSendDryAlert(req.Moisture, device.AutoWatering, device.LastDryAlertAtUtc, now, settings);
        if (sendDryAlert) device.LastDryAlertAtUtc = now;

        await db.SaveChangesAsync(ct);

        // Notifications are sent in the background: the device gets its answer immediately
        // even if RabbitMQ is slow or down (the ESP32 waits at most 2 seconds for a reply).
        if (device.OwnerUserId is Guid owner)
        {
            var messages = new List<NotificationRequested>();
            // While the tank is known to be empty, automatic runs pump nothing: do not report them as "watered"
            if (finishedRun is not null && !(device.TankEmpty && finishedRun.Trigger == PumpTriggers.Auto))
                messages.Add(PumpFinishedMessage(owner, device, finishedRun, now));
            if (tankAlert is { } alert) messages.Add(WaterTankMessage(owner, device, alert.Run, alert.MoistureNow, now));
            if (sendDryAlert) messages.Add(SoilDryMessage(owner, device, req.Moisture, now));
            foreach (var m in messages) _ = publisher.PublishAsync(m);
        }

        return new ReadingResponse(pumpSeconds, device.AutoWatering, settings.DryThreshold, settings.WetThreshold);
    }

    // Closes every run of this device that is still open (tracked, saved with the rest of the reading).
    private async Task CloseOpenRunsAsync(string deviceId, DateTime endedAtUtc, CancellationToken ct)
    {
        var open = await db.PumpRuns.Where(p => p.DeviceId == deviceId && p.EndedAtUtc == null).ToListAsync(ct);
        open.RemoveAll(r => r.EndedAtUtc is not null);      // already closed earlier in this same reading (not saved yet)
        foreach (var run in open)
            run.EndedAtUtc = endedAtUtc > run.StartedAtUtc ? endedAtUtc : run.StartedAtUtc;
        if (open.Count > 0)
            logger.LogInformation("Closed {Count} unfinished pump run(s) of {DeviceId}", open.Count, deviceId);
    }

    private static void ClearTankCheck(Device device)
    {
        device.TankCheckRunId = null;
        device.TankCheckDueAtUtc = null;
    }

    private static void ClearCommand(Device device)
    {
        device.PendingPumpSeconds = null;
        device.PendingPumpAtUtc = null;
    }

    private static NotificationRequested PumpFinishedMessage(Guid owner, Device device, PumpRun run, DateTime now)
    {
        var runTime = (run.EndedAtUtc ?? now) - run.StartedAtUtc;
        var seconds = Math.Max(1, (int)Math.Round(runTime.TotalSeconds));
        return new NotificationRequested(
            UserId: owner,
            Type: NotificationTypes.Pump,
            Title: "Plant watered",
            Body: $"{device.Name} was watered for {seconds}s. Soil moisture is now {run.MoistureAfter}%.",
            Data: new Dictionary<string, string>
            {
                ["device"] = device.Name,
                ["seconds"] = seconds.ToString(CultureInfo.InvariantCulture),
                ["moisture"] = (run.MoistureAfter ?? 0).ToString(CultureInfo.InvariantCulture),
                ["trigger"] = run.Trigger,
            },
            DedupKey: $"pump:{device.Id}:{run.Id}",
            OccurredAtUtc: now);
    }

    private static NotificationRequested WaterTankMessage(Guid owner, Device device, PumpRun run, int moistureNow, DateTime now)
    {
        var seconds = Math.Max(1, (int)Math.Round(((run.EndedAtUtc ?? now) - run.StartedAtUtc).TotalSeconds));
        return new NotificationRequested(
            UserId: owner,
            Type: NotificationTypes.WaterTank,
            Title: "Check the water tank",
            Body: $"The pump on {device.Name} ran for {seconds}s but the soil is still {moistureNow}%. The tank may be empty.",
            Data: new Dictionary<string, string>
            {
                ["device"] = device.Name,
                ["seconds"] = seconds.ToString(CultureInfo.InvariantCulture),
                ["moisture"] = moistureNow.ToString(CultureInfo.InvariantCulture),
                ["trigger"] = run.Trigger,
            },
            DedupKey: $"tank:{device.Id}:{run.Id}",
            OccurredAtUtc: now);
    }

    private static NotificationRequested SoilDryMessage(Guid owner, Device device, int moisture, DateTime now) => new(
        UserId: owner,
        Type: NotificationTypes.SoilDry,
        Title: "Your plant is thirsty",
        Body: $"Soil moisture on {device.Name} is {moisture}%. Water it soon or turn on auto-watering.",
        Data: new Dictionary<string, string>
        {
            ["device"] = device.Name,
            ["moisture"] = moisture.ToString(CultureInfo.InvariantCulture),
        },
        DedupKey: $"dry:{device.Id}:{now:yyyyMMddHH}",
        OccurredAtUtc: now);
}
