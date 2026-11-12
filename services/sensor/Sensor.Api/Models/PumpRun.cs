namespace Sensor.Api.Models;

// One time the pump switched on and off. Used for "Last watered", the activity list
// and the pump markers on the 24-hour chart.
public class PumpRun
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = "";
    public string Trigger { get; set; } = PumpTriggers.Auto;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }            // null while the pump is still running
    public int? MoistureBefore { get; set; }
    public int? MoistureAfter { get; set; }
}

public static class PumpTriggers
{
    public const string Auto = "auto";                   // the ESP32 decided by itself (soil too dry)
    public const string Manual = "manual";               // a user pressed "Water now" in the app
}
