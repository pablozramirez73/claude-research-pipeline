using Microsoft.EntityFrameworkCore;
using VitalFace.Core.Models;
using VitalFace.Infrastructure.Persistence.Configurations;

namespace VitalFace.Infrastructure.Persistence;

public sealed class VitalFaceDbContext(DbContextOptions<VitalFaceDbContext> options) : DbContext(options)
{
    public DbSet<Screening> Screenings => Set<Screening>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ScreeningConfiguration());
    }
}
