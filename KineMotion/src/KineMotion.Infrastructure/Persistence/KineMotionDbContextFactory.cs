using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KineMotion.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` run without spinning up the full Api host/DI container.</summary>
public sealed class KineMotionDbContextFactory : IDesignTimeDbContextFactory<KineMotionDbContext>
{
    public KineMotionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KineMotionDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=kinemotion;Username=kinemotion;Password=design-time-only");
        return new KineMotionDbContext(optionsBuilder.Options);
    }
}
