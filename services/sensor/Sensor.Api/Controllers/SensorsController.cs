using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sensor.Api.DTOs;
using Sensor.Api.Services;

namespace Sensor.Api.Controllers;

// Endpoint used by the ESP32 and the simulator. Devices have no user account,
// so instead of a JWT they authenticate with their own key in the X-Device-Key header.
[ApiController]
[Route("api/sensors")]
[AllowAnonymous]
public class SensorsController(ReadingIngestor ingestor) : ControllerBase
{
    // POST /api/sensors/readings
    // Header: X-Device-Key: <DEVICE_KEY from firmware config.h>
    // Body:   { "deviceId": "TREEO_ESP32_01", "moisture": 42, "pumpOn": false, "wifiRssi": -58, "firmware": "1.1.0" }
    // Reply:  { "pumpSeconds": 0, "autoWatering": true, "dryThreshold": 30, "wetThreshold": 60 }
    [HttpPost("readings")]
    public async Task<ActionResult<ReadingResponse>> PostReading([FromBody] ReadingRequest req, CancellationToken ct)
    {
        var key = Request.Headers[DeviceKeys.Header].FirstOrDefault();
        // Through the gateway the connection comes from the gateway itself; YARP passes the device's
        // real address in X-Forwarded-For (first entry). Only used for display in the app.
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var ip = !string.IsNullOrEmpty(forwarded) && forwarded.Length <= 45
            ? forwarded
            : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();

        var response = await ingestor.IngestAsync(req, key, ip, ct);
        if (response is null) return Unauthorized(new { message = "Unknown device or wrong device key." });

        return Ok(response);
    }
}
