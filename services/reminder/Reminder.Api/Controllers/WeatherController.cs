using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using TreeO.Contracts.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reminder.Api.DTOs;
using Reminder.Api.Messaging;
using Reminder.Api.Services;

namespace Reminder.Api.Controllers;

// Can dang nhap (gui kem token) vi API nay con gui thong bao nang nong cho dung nguoi dang goi.
[ApiController]
[Route("api/weather")]
[Authorize]
public class WeatherController(
    WeatherService weather,
    INotificationPublisher publisher,
    IConfiguration config,
    ILogger<WeatherController> logger) : ControllerBase
{
    // Nho cac thong bao nang nong da gui (theo nguoi + ngay) de khong gui lai moi lan mo app.
    // Neu co bi mat khi khoi dong lai service thi Notification service van chan trung nho DedupKey.
    private static readonly ConcurrentDictionary<string, byte> SentAlerts = new();

    // GET /api/weather                         -> thoi tiet o dia diem mac dinh (TP.HCM)
    // GET /api/weather?lat=21.03&lon=105.85&name=Ha%20Noi
    [HttpGet]
    public async Task<ActionResult<WeatherReportDto>> Get(
        [FromQuery] double? lat, [FromQuery] double? lon, [FromQuery] string? name, CancellationToken ct)
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();

        var latitude = lat ?? config.GetValue("Weather:DefaultLatitude", 10.8231);
        var longitude = lon ?? config.GetValue("Weather:DefaultLongitude", 106.6297);
        if (double.IsNaN(latitude) || double.IsNaN(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return BadRequest(new ErrorResponse("Invalid coordinates."));
        }

        var trimmed = (name ?? "").Trim();
        var locationName = trimmed.Length == 0
            ? config["Weather:DefaultName"] ?? "Ho Chi Minh City"
            : trimmed[..Math.Min(trimmed.Length, 80)];

        WeatherReportDto report;
        try
        {
            report = await weather.GetAsync(latitude, longitude, locationName, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            logger.LogWarning("Khong lay duoc thoi tiet: {Message}", ex.Message);
            return StatusCode(502, new ErrorResponse("Weather data is unavailable right now. Please try again in a moment."));
        }

        SendHeatwaveAlertIfNeeded(me, report);
        return Ok(report);
    }

    // Du bao ngay mai nang nong -> gui 1 thong bao qua RabbitMQ (moi nguoi, moi ngay 1 lan)
    private void SendHeatwaveAlertIfNeeded(Guid userId, WeatherReportDto report)
    {
        if (!report.Heatwave.Tomorrow || report.Forecast.Count < 2) return;

        var tomorrow = report.Forecast[1];
        var dedupKey = $"heatwave:{userId}:{tomorrow.Date}";
        if (!SentAlerts.TryAdd(dedupKey, 0)) return;
        if (SentAlerts.Count > 5000) SentAlerts.Clear();   // tranh phinh bo nho khi chay lau

        var maxTemp = (report.Heatwave.TomorrowMaxC ?? tomorrow.MaxC).ToString("0.#", CultureInfo.InvariantCulture);
        var message = new NotificationRequested(
            UserId: userId,
            Type: NotificationTypes.Heatwave,
            Title: "Heatwave tomorrow",
            Body: $"Up to {maxTemp}°C expected in {report.LocationName}. Water your plants a bit more.",
            Data: new Dictionary<string, string>
            {
                ["maxTemp"] = maxTemp,
                ["date"] = tomorrow.Date,
                ["location"] = report.LocationName,
            },
            DedupKey: dedupKey,
            OccurredAtUtc: DateTime.UtcNow);

        // Khong cho gui xong moi tra thoi tiet cho app (RabbitMQ cham/tat khong duoc lam cham man hinh).
        // PublishAsync khong bao gio nem loi; gui that bai thi bo khoa de lan mo app sau thu lai.
        _ = publisher.PublishAsync(message, CancellationToken.None).ContinueWith(
            t =>
            {
                if (t.IsFaulted || !t.Result) SentAlerts.TryRemove(dedupKey, out _);
            },
            TaskScheduler.Default);
    }
}
