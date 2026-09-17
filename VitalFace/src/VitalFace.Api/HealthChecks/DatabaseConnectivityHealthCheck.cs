using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VitalFace.Infrastructure.Persistence;

namespace VitalFace.Api.HealthChecks;

/// <summary>
/// Unlike the built-in <c>AddDbContextCheck</c> (which calls <c>CanConnectAsync</c> — a method
/// that swallows the underlying connection exception and only returns true/false), this attaches
/// the real exception to the health report, so `/health` can surface *why* the database is
/// unreachable (bad credentials, DB down, etc.) instead of a bare "Unhealthy".
/// </summary>
public sealed class DatabaseConnectivityHealthCheck(VitalFaceDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Impossibile connettersi al database.", ex);
        }
    }
}
