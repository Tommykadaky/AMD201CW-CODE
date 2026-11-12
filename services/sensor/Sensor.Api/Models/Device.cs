namespace Sensor.Api.Models;

// One ESP32 board (or the simulator). Holds the latest live values so the app
// can read "moisture right now" from a single row instead of scanning readings.
public class Device
{
    public string Id { get; set; } = "";                 // device_id written in firmware config, e.g. TREEO_ESP32_01
    public string KeyHash { get; set; } = "";            // SHA-256 of the device key (the key itself is never stored)
    public Guid? OwnerUserId { get; set; }               // null until a user pairs the device in the app
    public string Name { get; set; } = "My plant";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ----- Latest live values (updated on every reading) -----
    public DateTime? LastSeenAtUtc { get; set; }
    public int? Moisture { get; set; }
    public bool PumpOn { get; set; }
    public string? IpAddress { get; set; }
    public int? WifiRssi { get; set; }
    public string? Firmware { get; set; }

    // ----- Settings the app can change; sent back to the device in every response -----
    public bool AutoWatering { get; set; } = true;

    // ----- Manual "Water now" command waiting to be picked up by the device -----
    public int? PendingPumpSeconds { get; set; }
    public DateTime? PendingPumpAtUtc { get; set; }
    public DateTime? CommandSentAtUtc { get; set; }      // when the device received the last command

    // ----- Bookkeeping for storage throttling and alerts -----
    public DateTime? LastStoredAtUtc { get; set; }
    public int? LastStoredMoisture { get; set; }
    public DateTime? LastDryAlertAtUtc { get; set; }

    // ----- Delayed "empty water tank" check (see ReadingIngestor) -----
    public long? TankCheckRunId { get; set; }            // pump run waiting to be checked
    public DateTime? TankCheckDueAtUtc { get; set; }     // when the soil should have absorbed the water
    public bool TankEmpty { get; set; }                  // last check failed; cleared when a later run wets the soil
}
