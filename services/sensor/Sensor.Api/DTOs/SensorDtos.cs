using System.ComponentModel.DataAnnotations;

namespace Sensor.Api.DTOs;

// ===== Device -> server =====

// Sent by the ESP32 (or the simulator) every few seconds to POST /api/sensors/readings
public record ReadingRequest(
    [Required, StringLength(64, MinimumLength = 3), RegularExpression("^[A-Za-z0-9_-]+$")] string DeviceId,
    [Range(0, 100)] int Moisture,
    bool PumpOn,
    [Range(-120, 0)] int? WifiRssi,
    [StringLength(30)] string? Firmware);

// Reply to the device: a pending manual command (0 = none) plus the settings it must follow
public record ReadingResponse(
    int PumpSeconds,
    bool AutoWatering,
    int DryThreshold,
    int WetThreshold);

// ===== App -> server =====

public record ClaimDeviceRequest(
    [Required, StringLength(64, MinimumLength = 3), RegularExpression("^[A-Za-z0-9_-]+$")] string DeviceId,
    [Required, StringLength(128, MinimumLength = 16)] string DeviceKey,
    [StringLength(60)] string? Name);

public record UpdateDeviceRequest(
    [StringLength(60, MinimumLength = 1)] string? Name,
    bool? AutoWatering);

public record WaterRequest([Range(1, 60)] int? Seconds);

// ===== Server -> app =====

public record DeviceDto(
    string Id,
    string Name,
    bool Online,
    DateTime? LastSeenAtUtc,
    int? Moisture,
    bool PumpOn,
    bool AutoWatering,
    bool WateringPending,
    string? IpAddress,
    int? WifiRssi,
    string? Firmware,
    int DryThreshold,
    int WetThreshold,
    DateTime? LastWateredAtUtc,
    bool TankEmpty);                                     // the last pump run did not wet the soil

public record WaterResponse(int Seconds, string Message);

public record HistoryPointDto(DateTime HourStartUtc, int? Average, int? Min, int? Max);

public record PumpRunDto(
    long Id,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int? DurationSeconds,
    int? MoistureBefore,
    int? MoistureAfter);

public record HistoryDto(int Hours, List<HistoryPointDto> Points, List<PumpRunDto> PumpRuns);
