using KineMotion.Api.Endpoints;
using KineMotion.Api.Hubs;
using KineMotion.Infrastructure;
using KineMotion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("KineMotion")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:KineMotion.");
builder.Services.AddKineMotionInfrastructure(connectionString);

builder.Services.AddSignalR();

var patientOrigins = builder.Configuration.GetSection("Cors:PatientOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("KineMotionClients", policy =>
{
    if (patientOrigins.Length > 0)
    {
        policy.WithOrigins(patientOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KineMotionDbContext>();
    // Deliberate Development-only auto-migrate — see KineMotion/README.md "Sicurezza e produzione":
    // running EF migrations automatically at container startup is a real production risk (races
    // across replicas, uncontrolled downtime), so the real release process should apply them as
    // an explicit `dotnet ef database update` step instead.
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCors("KineMotionClients");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapPrescriptionsEndpoints();
app.MapSessionsEndpoints();
app.MapHub<RehabHub>("/hubs/rehab");

app.Run();
