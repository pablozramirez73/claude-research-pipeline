using KineMotion.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace KineMotion.Infrastructure.Persistence;

public sealed class KineMotionDbContext(DbContextOptions<KineMotionDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KineMotionDbContext).Assembly);
    }
}
