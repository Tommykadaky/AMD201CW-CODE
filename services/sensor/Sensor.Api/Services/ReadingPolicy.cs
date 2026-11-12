using Sensor.Api.Models;

namespace Sensor.Api.Services;

// All decisions of the Sensor service as pure functions: no database, no RabbitMQ, no clock.
// The caller passes "now" in, which makes every rule easy to unit test (see Sensor.Tests).
public static class ReadingPolicy
{
    // Should this reading be written to the Readings table?
    // Storing every 5-second sample would add ~17,000 rows per device per day; this keeps ~1,500
    // while still capturing every real change (pump on/off, a jump in moisture).
    public static bool ShouldStore(
        DateTime? lastStoredAtUtc,
        int? lastStoredMoisture,
        bool previousPumpOn,
        int moisture,
        bool pumpOn,
        DateTime nowUtc,
        SensorSettings s)
    {
        if (lastStoredAtUtc is null || lastStoredMoisture is null) return true;   // first reading ever
        if (pumpOn != previousPumpOn) return true;                                 // pump switched
        if (Math.Abs(moisture - lastStoredMoisture.Value) >= s.StoreOnChangePercent) return true;
        return nowUtc - lastStoredAtUtc.Value >= TimeSpan.FromSeconds(s.StoreEverySeconds);
    }

    public static bool IsOnline(DateTime? lastSeenAtUtc, DateTime nowUtc, SensorSettings s) =>
        lastSeenAtUtc is not null && nowUtc - lastSeenAtUtc.Value <= TimeSpan.FromSeconds(s.OnlineWindowSeconds);

    // The manual pump command still waiting for the device, or null if there is none / it expired.
    public static int? ActiveCommand(int? pendingSeconds, DateTime? pendingAtUtc, DateTime nowUtc, SensorSettings s)
    {
        if (pendingSeconds is not > 0 || pendingAtUtc is null) return null;
        return nowUtc - pendingAtUtc.Value <= TimeSpan.FromSeconds(s.CommandTimeoutSeconds) ? pendingSeconds : null;
    }

    // A pending command is only handed to the device when it can act on it. The firmware ignores
    // "Water now" while the pump is already running or above the hard stop; handing it over anyway
    // would silently drop it and leave the app waiting. While the pump runs, the command stays pending
    // and is delivered with the first reading after the pump stops (if it has not expired by then).
    public static bool CanDeliverCommand(bool pumpOn, int moisture, SensorSettings s) =>
        !pumpOn && moisture < s.HardStopPercent;

    // Soil already at or above the hard stop: the device would refuse to pump.
    public static bool IsTooWetToWater(int? moisture, SensorSettings s) =>
        moisture is not null && moisture.Value >= s.HardStopPercent;

    // A device that was offline (no reading within the online window) cannot be trusted to still be
    // in the state it last reported, e.g. it may have rebooted in the middle of a pump run.
    public static bool WasOffline(DateTime? lastSeenAtUtc, DateTime nowUtc, SensorSettings s) =>
        lastSeenAtUtc is not null && !IsOnline(lastSeenAtUtc, nowUtc, s);

    public static bool IsTankCheckDue(DateTime? dueAtUtc, DateTime nowUtc) =>
        dueAtUtc is not null && nowUtc >= dueAtUtc.Value;

    // Seconds for a manual watering: default when not given, always between 1 and the configured maximum.
    public static int ClampPumpSeconds(int? requested, SensorSettings s) =>
        Math.Clamp(requested ?? s.ManualPumpDefaultSeconds, 1, s.ManualPumpMaxSeconds);

    // A pump that starts shortly after the device received a command was started by that command.
    public static string ClassifyTrigger(DateTime? commandSentAtUtc, DateTime pumpStartedAtUtc)
    {
        if (commandSentAtUtc is null) return PumpTriggers.Auto;
        var gap = pumpStartedAtUtc - commandSentAtUtc.Value;
        return gap >= TimeSpan.Zero && gap <= TimeSpan.FromSeconds(20) ? PumpTriggers.Manual : PumpTriggers.Auto;
    }

    // "Your plant is thirsty" is only useful when nothing will water it automatically,
    // and it must not repeat every 5 seconds.
    public static bool ShouldSendDryAlert(int moisture, bool autoWatering, DateTime? lastAlertAtUtc, DateTime nowUtc, SensorSettings s)
    {
        if (autoWatering || moisture >= s.DryThreshold) return false;
        return lastAlertAtUtc is null || nowUtc - lastAlertAtUtc.Value >= TimeSpan.FromHours(s.DryAlertCooldownHours);
    }

    // Runs shorter than this are never used for the tank check (not enough time for water to arrive).
    public static readonly TimeSpan MinRunForTankCheck = TimeSpan.FromSeconds(3);

    // The soil is clearly wetter than before the run: water is reaching the plant.
    public static bool SoilGotWetter(int? moistureBefore, int? moistureAfter) =>
        moistureBefore is not null && moistureAfter is not null && moistureAfter.Value - moistureBefore.Value >= 2;

    // The pump ran for a few seconds but the soil did not get wetter: the water tank is probably empty
    // or the tube came off. moistureAfter is measured TankCheckDelaySeconds after the run, not at
    // pump-off (see ReadingIngestor).
    public static bool LooksLikeEmptyTank(int? moistureBefore, int? moistureAfter, TimeSpan runTime) =>
        moistureBefore is not null && moistureAfter is not null
        && runTime >= MinRunForTankCheck
        && moistureAfter.Value - moistureBefore.Value < 2;
}
