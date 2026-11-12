using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sensor.Api.Data;
using Sensor.Api.Messaging;
using Sensor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Database (Postgres in Docker): sensor_db =====
builder.Services.AddDbContext<SensorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ===== JWT: only VALIDATES tokens issued by Identity; Issuer/Audience/Key must match Identity =====
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters and identical to the Identity service (see appsettings.Development.json).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ===== CORS: websites allowed to call this API (only needed when the app runs in a browser) =====
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("web", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// ===== Sensor rules (thresholds, storage throttling, retention) =====
var sensorSettings = builder.Configuration.GetSection("Sensor").Get<SensorSettings>() ?? new SensorSettings();
if (sensorSettings.DryThreshold >= sensorSettings.WetThreshold)
    throw new InvalidOperationException("Sensor:DryThreshold must be lower than Sensor:WetThreshold.");
if (sensorSettings.WetThreshold >= sensorSettings.HardStopPercent)
    throw new InvalidOperationException("Sensor:WetThreshold must be lower than Sensor:HardStopPercent.");
if (sensorSettings.TankCheckDelaySeconds is < 5 or >= 60)
    throw new InvalidOperationException("Sensor:TankCheckDelaySeconds must be between 5 and 59 (the firmware rests 60 s between automatic runs).");
builder.Services.AddSingleton(sensorSettings);
builder.Services.AddScoped<ReadingIngestor>();
builder.Services.AddHostedService<ReadingCleanupService>();

// ===== RabbitMQ: pump / dry soil / water tank notifications =====
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqSettings>() ?? new RabbitMqSettings();
builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton<INotificationPublisher, RabbitMqNotificationPublisher>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ===== Create / update tables on startup =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
    if (!db.Database.GetMigrations().Any())
        throw new InvalidOperationException(
            "No migrations found. Run once in services/sensor/Sensor.Api:  dotnet ef migrations add InitialCreate -o Data/Migrations");
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: the ESP32 talks plain HTTP on the local network and cannot follow a redirect to HTTPS
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Quick liveness check (no login needed)
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "sensor" }));

app.Run();
