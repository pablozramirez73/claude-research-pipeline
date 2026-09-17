using Microsoft.EntityFrameworkCore;
using VitalFace.Api.Endpoints;
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
    .AddDbContextCheck<VitalFaceDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("KioskClients");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Applies pending EF Core migrations automatically in local/dev only — production
    // deployments run migrations explicitly as a release step (see Dockerfile / CI).
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<VitalFaceDbContext>().Database.MigrateAsync();
}

app.MapVitalsEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program; // exposed for WebApplicationFactory-based integration tests
