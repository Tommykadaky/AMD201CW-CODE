namespace Sensor.Api.Models;

// A stored moisture sample. Not every 5-second reading is stored: see ReadingPolicy.ShouldStore.
public class SensorReading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = "";
    public int Moisture { get; set; }                    // 0-100 %
    public bool PumpOn { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
