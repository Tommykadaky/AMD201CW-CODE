namespace Sensor.Api.Services;

// Read from the "Sensor" section of appsettings.json. Every number the system depends on lives here,
// so the team can tune behaviour (e.g. thresholds for a different plant) without touching code.
public class SensorSettings
{
    // Moisture thresholds (%). Below DryThreshold the soil needs water; the pump stops at WetThreshold.
    // Both values are sent to the device in every response, so the ESP32 always uses the server's numbers.
    public int DryThreshold { get; set; } = 30;
    public int WetThreshold { get; set; } = 60;

    // Storage throttling: the device reports every 5 s, but a row is only stored when
    // StoreEverySeconds have passed, the moisture moved by StoreOnChangePercent, or the pump switched.
    public int StoreEverySeconds { get; set; } = 60;
    public int StoreOnChangePercent { get; set; } = 3;

    // A device that has not reported for this long is shown as offline.
    public int OnlineWindowSeconds { get; set; } = 30;

    // Manual "Water now": how long the pump runs, the upper limit, and how long a command
    // waits for the device before it is dropped (never water an hour late).
    public int ManualPumpDefaultSeconds { get; set; } = 5;
    public int ManualPumpMaxSeconds { get; set; } = 15;
    public int CommandTimeoutSeconds { get; set; } = 60;

    // The device never pumps above this moisture (same value as HARD_STOP_PERCENT in the firmware),
    // so the server refuses a "Water now" command it knows the device would ignore.
    public int HardStopPercent { get; set; } = 85;

    // At most one "soil is dry" notification per device in this window.
    public int DryAlertCooldownHours { get; set; } = 6;

    // How long after a pump run the "is the water tank empty?" check waits. Water needs time to soak
    // down to the sensor, and the firmware smooths readings, so moisture right at pump-off is too early.
    // Must stay below the firmware's 60 s rest between automatic runs, so the check happens before the next run.
    public int TankCheckDelaySeconds { get; set; } = 45;

    // Readings and pump runs older than this are deleted by ReadingCleanupService.
    public int RetentionDays { get; set; } = 30;

    // true: an unknown device_id is registered on its first reading (handy for the simulator).
    // false: a user must pair the device in the app before readings are accepted.
    public bool AllowAutoRegister { get; set; } = true;
}
