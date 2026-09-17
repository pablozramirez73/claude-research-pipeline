using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using VitalFace.Api.Endpoints;
using VitalFace.Api.HealthChecks;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Services;
using VitalFace.Infrastructure.DependencyInjection;
using VitalFace.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IRppgProcessor, RppgProcessor>();
builder.Services.AddSingleton<IFatigueDetector, FatigueDetector>();
builder.Services.AddSingleton<IRespiratoryRateEstimator, RespiratoryRateEstimator>();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");
builder.Services.AddVitalFaceInfrastructure(connectionString);

var kioskOrigins = builder.Configuration.GetSection("Cors:KioskOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("KioskClients", policy =>
    {
        if (kioskOrigins.Length > 0)
            policy.WithOrigins(kioskOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseConnectivityHealthCheck>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("KioskClients");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Applies pending EF Core migrations at startup. Defaults to Development, but is explicitly
// opted into for the single-instance docker-compose/tunnel-script deployment (see
// docker-compose.yml's AutoMigrate=true) purely for convenience — a real multi-replica
// production rollout should turn this off and run migrations as an explicit release step
// instead (a rolling deploy with several instances racing to apply the same migration is a
// real risk this flag is not meant to cover).
var shouldAutoMigrate = builder.Configuration.GetValue<bool?>("AutoMigrate") ?? app.Environment.IsDevelopment();
if (shouldAutoMigrate)
{
    using var scope = app.Services.CreateScope();
    try
    {
        await scope.ServiceProvider.GetRequiredService<VitalFaceDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Don't let a migration failure (e.g. bad DB credentials, DB unreachable) crash the whole
        // app before it can even serve a request — keep starting so /health reports the same
        // underlying error immediately and diagnosably, instead of an unhandled-exception crash
        // with no HTTP surface to inspect at all.
        app.Logger.LogError(ex, "Failed to apply EF Core migrations at startup.");
    }
}

app.MapVitalsEndpoints();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        // The default health check response is just the bare status word ("Healthy" /
        // "Unhealthy"), which is useless for diagnosing *why* — e.g. a DB auth failure vs. the
        // database being unreachable vs. migrations not having run. Surface each check's
        // exception message instead.
        context.Response.ContentType = "application/json";
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                error = entry.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(payload);
    }
});

app.Run();

public partial class Program; // exposed for WebApplicationFactory-based integration tests
