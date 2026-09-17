using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VitalFace.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory so `dotnet ef migrations add` can build the model without a running
/// ASP.NET host or a real connection string. Never used at runtime — VitalFace.Api registers the
/// real, environment-specific connection string via <c>AddVitalFaceInfrastructure</c>.
/// </summary>
public sealed class VitalFaceDbContextFactory : IDesignTimeDbContextFactory<VitalFaceDbContext>
{
    public VitalFaceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VitalFaceDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=vitalface;Username=vitalface;Password=design-time-only");
        return new VitalFaceDbContext(optionsBuilder.Options);
    }
}
