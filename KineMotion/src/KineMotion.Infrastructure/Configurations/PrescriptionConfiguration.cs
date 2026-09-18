using KineMotion.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KineMotion.Infrastructure.Configurations;

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Exercise).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(p => new { p.PatientId, p.Active });
    }
}
