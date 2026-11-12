using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

// ===========================================================================
// API Gateway: the single entry point of the system.
//
//   app / ESP32  ──>  Gateway (5100)  ──>  Identity, Forum, Garden, Reminder,
//                                         Notification, AI Assistant, Sensor
//
// - Routing: which URL goes to which service is defined in appsettings.json (section "ReverseProxy").
// - Rate limiting: login / register / password reset are limited per IP (brute-force protection).
// - Health: /health (gateway only) and /health/all (asks every service).
// - CORS and JWT checks stay in each service; the gateway passes those headers through unchanged.
// ===========================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ===== Rate limiting (the policy name is set per route in appsettings.json: "RateLimiterPolicy") =====
var authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10);
var defaultPerMinute = builder.Configuration.GetValue("RateLimits:DefaultPerMinute", 300);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please wait a minute and try again.\"}", ct);
    };

    // Sensitive endpoints: a few attempts per minute per IP address
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Everything else: generous, only stops runaway clients (the app polls every 5 s)
    options.AddPolicy("default", http => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = defaultPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHttpClient("health", client => client.Timeout = TimeSpan.FromSeconds(3));

var app = builder.Build();

app.UseRateLimiter();

// The gateway itself is alive
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));

// Asks every service behind the gateway for its /health (used in the demo and by deploy scripts).
// 200 when all are up, 503 when at least one is down.
app.MapGet("/health/all", async (IHttpClientFactory factory, IConfiguration config, CancellationToken ct) =>
{
    var http = factory.CreateClient("health");
    var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren().ToList();

    var checks = clusters.Select(async cluster =>
    {
        var address = cluster.GetSection("Destinations").GetChildren().FirstOrDefault()?["Address"];
        if (string.IsNullOrWhiteSpace(address)) return new ServiceHealth(cluster.Key, "not configured", null);
        try
        {
            using var res = await http.GetAsync(new Uri(new Uri(address), "/health"), ct);
            return new ServiceHealth(cluster.Key, res.IsSuccessStatusCode ? "ok" : $"error {(int)res.StatusCode}", address);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ServiceHealth(cluster.Key, "down", address);
        }
    });

    var results = await Task.WhenAll(checks);
    var allOk = results.All(r => r.Status == "ok");
    return Results.Json(
        new { status = allOk ? "ok" : "degraded", services = results },
        statusCode: allOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapReverseProxy();

app.Run();

// Rate-limit bucket per client IP address
static string ClientKey(HttpContext http) =>
    http.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();

record ServiceHealth(string Service, string Status, string? Address);
