// Plant Catalog service: placeholder, no features yet (not routed by the gateway, not in docker-compose).
// Planned: catalogue of small desk / balcony plants with care defaults used by reminders and the AI care plan.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "plant-catalog" }));

app.Run();
