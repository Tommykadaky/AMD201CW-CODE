using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Sensor.Api.Data;
using Sensor.Api.DTOs;
using Sensor.Api.Models;
using Sensor.Api.Services;

namespace Sensor.Api.Controllers;

// Endpoints used by the app. Every call needs a JWT from the Identity service,
// and users only ever see devices they have paired themselves.
[ApiController]
[Route("api/sensors/devices")]
[Authorize]
public class DevicesController(SensorDbContext db, SensorSettings settings) : ControllerBase
{
    // GET /api/sensors/devices   -> my devices with their live values
    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> List(CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();

        var devices = await db.Devices.AsNoTracking()
            .Where(d => d.OwnerUserId == me)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);

        var ids = devices.Select(d => d.Id).ToList();
        var lastWatered = await db.PumpRuns.AsNoTracking()
            .Where(p => ids.Contains(p.DeviceId))
            .GroupBy(p => p.DeviceId)
            .Select(g => new { DeviceId = g.Key, At = g.Max(p => p.StartedAtUtc) })
            .ToDictionaryAsync(x => x.DeviceId, x => x.At, ct);

        var now = DateTime.UtcNow;
        return Ok(devices.Select(d => ToDto(d, lastWatered.TryGetValue(d.Id, out var at) ? at : null, now)).ToList());
    }

    // GET /api/sensors/devices/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> Get(string id, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        var device = await FindMineAsync(id, me, ct);
        if (device is null) return DeviceNotFound();

        return Ok(ToDto(device, await LastWateredAsync(id, ct), DateTime.UtcNow));
    }

    // POST /api/sensors/devices   { "deviceId": "...", "deviceKey": "...", "name": "Money Tree" }
    // Pairs a device with my account. Works before or after the device has sent its first reading.
    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Claim([FromBody] ClaimDeviceRequest req, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();

        var name = string.IsNullOrWhiteSpace(req.Name) ? "My plant" : req.Name.Trim();
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == req.DeviceId, ct);

        if (device is null)
        {
            device = new Device { Id = req.DeviceId, KeyHash = DeviceKeys.Hash(req.DeviceKey), OwnerUserId = me, Name = name };
            db.Devices.Add(device);
        }
        else
        {
            // Same message for "wrong key" and "no such device", so nobody can probe which IDs exist
            if (!DeviceKeys.Matches(req.DeviceKey, device.KeyHash))
                return BadRequest(new { message = "Device ID or device key is wrong." });
            if (device.OwnerUserId is Guid owner && owner != me)
                return Conflict(new { message = "This device is already paired with another account." });

            device.OwnerUserId = me;
            device.Name = name;
        }

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(device, await LastWateredAsync(device.Id, ct), DateTime.UtcNow));
    }

    // PATCH /api/sensors/devices/{id}   { "name": "Desk plant", "autoWatering": false }
    // The new autoWatering value reaches the ESP32 in the reply to its next reading (within ~5 s).
    [HttpPatch("{id}")]
    public async Task<ActionResult<DeviceDto>> Update(string id, [FromBody] UpdateDeviceRequest req, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        var device = await FindMineAsync(id, me, ct, track: true);
        if (device is null) return DeviceNotFound();

        if (!string.IsNullOrWhiteSpace(req.Name)) device.Name = req.Name.Trim();
        if (req.AutoWatering is bool auto) device.AutoWatering = auto;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(device, await LastWateredAsync(id, ct), DateTime.UtcNow));
    }

    // DELETE /api/sensors/devices/{id}   -> unpair and delete this device's history
    [HttpDelete("{id}")]
    public async Task<IActionResult> Unpair(string id, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        var device = await FindMineAsync(id, me, ct, track: true);
        if (device is null) return DeviceNotFound();

        // The next owner must not see the previous owner's data
        await db.Readings.Where(r => r.DeviceId == id).ExecuteDeleteAsync(ct);
        await db.PumpRuns.Where(p => p.DeviceId == id).ExecuteDeleteAsync(ct);

        device.OwnerUserId = null;
        device.Name = device.Id;
        device.PendingPumpSeconds = null;
        device.PendingPumpAtUtc = null;
        device.LastStoredAtUtc = null;
        device.LastStoredMoisture = null;
        device.LastDryAlertAtUtc = null;
        device.CommandSentAtUtc = null;
        device.AutoWatering = true;                       // the next owner starts with the default settings
        device.TankCheckRunId = null;
        device.TankCheckDueAtUtc = null;
        device.TankEmpty = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // GET /api/sensors/devices/{id}/history?hours=24   -> one point per hour + pump runs, for the chart
    [HttpGet("{id}/history")]
    public async Task<ActionResult<HistoryDto>> History(string id, [FromQuery] int hours = 24, CancellationToken ct = default)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        if (await FindMineAsync(id, me, ct) is null) return DeviceNotFound();

        hours = Math.Clamp(hours, 1, 168);
        var now = DateTime.UtcNow;
        // Same window as the chart: from the start of the first hourly bucket, so pump markers never fall outside it
        var from = HistoryBuilder.FirstHour(now, hours);

        // Only two columns are read; the (DeviceId, RecordedAtUtc) index serves this query
        var samples = await db.Readings.AsNoTracking()
            .Where(r => r.DeviceId == id && r.RecordedAtUtc >= from)
            .Select(r => new { r.RecordedAtUtc, r.Moisture })
            .ToListAsync(ct);

        var runs = await db.PumpRuns.AsNoTracking()
            .Where(p => p.DeviceId == id && p.StartedAtUtc >= from)
            .OrderBy(p => p.StartedAtUtc)
            .ToListAsync(ct);

        var points = HistoryBuilder.Hourly(samples.Select(s => (s.RecordedAtUtc, s.Moisture)), now, hours);
        return Ok(new HistoryDto(hours, points, runs.Select(ToRunDto).ToList()));
    }

    // GET /api/sensors/devices/{id}/activity?limit=10   -> latest pump runs, newest first
    [HttpGet("{id}/activity")]
    public async Task<ActionResult<List<PumpRunDto>>> Activity(string id, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        if (await FindMineAsync(id, me, ct) is null) return DeviceNotFound();

        var runs = await db.PumpRuns.AsNoTracking()
            .Where(p => p.DeviceId == id)
            .OrderByDescending(p => p.StartedAtUtc)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(ct);
        return Ok(runs.Select(ToRunDto).ToList());
    }

    // POST /api/sensors/devices/{id}/water   { "seconds": 5 }
    // Queues a command; the device picks it up with its next reading (every ~5 s).
    // The app then watches pumpOn go true -> false to know the watering finished.
    [HttpPost("{id}/water")]
    public async Task<ActionResult<WaterResponse>> Water(string id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WaterRequest? req, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();
        var device = await FindMineAsync(id, me, ct, track: true);
        if (device is null) return DeviceNotFound();

        var now = DateTime.UtcNow;
        if (!ReadingPolicy.IsOnline(device.LastSeenAtUtc, now, settings))
            return Conflict(new { message = "The device is offline. Check its power and Wi-Fi." });
        if (device.PumpOn)
            return Conflict(new { message = "The pump is already running." });
        if (ReadingPolicy.IsTooWetToWater(device.Moisture, settings))
            return Conflict(new { message = $"The soil is already wet ({device.Moisture}%). The pump will not run above {settings.HardStopPercent}%." });

        // Pressing the button twice must not queue a second run: return the command that is already waiting
        if (ReadingPolicy.ActiveCommand(device.PendingPumpSeconds, device.PendingPumpAtUtc, now, settings) is int waiting)
            return Accepted(new WaterResponse(waiting, "A watering command is already waiting for the device."));

        var seconds = ReadingPolicy.ClampPumpSeconds(req?.Seconds, settings);
        device.PendingPumpSeconds = seconds;
        device.PendingPumpAtUtc = now;
        await db.SaveChangesAsync(ct);

        return Accepted(new WaterResponse(seconds, "Watering command sent to the device."));
    }

    // ---------- helpers ----------

    private async Task<Device?> FindMineAsync(string id, Guid me, CancellationToken ct, bool track = false)
    {
        var query = track ? db.Devices : db.Devices.AsNoTracking();
        return await query.FirstOrDefaultAsync(d => d.Id == id && d.OwnerUserId == me, ct);
    }

    private async Task<DateTime?> LastWateredAsync(string deviceId, CancellationToken ct) =>
        await db.PumpRuns.AsNoTracking()
            .Where(p => p.DeviceId == deviceId)
            .OrderByDescending(p => p.StartedAtUtc)
            .Select(p => (DateTime?)p.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

    private NotFoundObjectResult DeviceNotFound() => NotFound(new { message = "Device not found." });

    private DeviceDto ToDto(Device d, DateTime? lastWateredAtUtc, DateTime now)
    {
        var online = ReadingPolicy.IsOnline(d.LastSeenAtUtc, now, settings);
        return new DeviceDto(
            d.Id,
            d.Name,
            online,
            d.LastSeenAtUtc,
            d.Moisture,
            online && d.PumpOn,                               // an offline device's last "pump on" is stale, not a running pump
            d.AutoWatering,
            ReadingPolicy.ActiveCommand(d.PendingPumpSeconds, d.PendingPumpAtUtc, now, settings) is not null,
            d.IpAddress,
            d.WifiRssi,
            d.Firmware,
            settings.DryThreshold,
            settings.WetThreshold,
            lastWateredAtUtc,
            d.TankEmpty);
    }

    private static PumpRunDto ToRunDto(PumpRun p) => new(
        p.Id,
        p.Trigger,
        p.StartedAtUtc,
        p.EndedAtUtc,
        p.EndedAtUtc is DateTime end ? Math.Max(1, (int)Math.Round((end - p.StartedAtUtc).TotalSeconds)) : null,
        p.MoistureBefore,
        p.MoistureAfter);
}
