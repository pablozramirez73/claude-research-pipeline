using KineMotion.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KineMotion.Infrastructure.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id);

        // Frame series is always read/written whole (never filtered per-frame in SQL), so it is
        // stored as a single JSON column rather than a child table — see brief §7: "EF Core 11
        // con JSON columns per metriche" (modelBuilder.Entity<Session>().OwnsMany(...).ToJson()).
        builder.OwnsMany(s => s.Frames, framesBuilder => framesBuilder.ToJson());

        builder.HasIndex(s => new { s.PatientId, s.StartedAtUtc });
    }
}
