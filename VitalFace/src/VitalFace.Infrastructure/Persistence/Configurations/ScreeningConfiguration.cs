using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VitalFace.Core.Models;

namespace VitalFace.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API mapping for <see cref="Screening"/>. Kept out of the domain entity itself (no EF
/// attributes on <see cref="Screening"/>) so VitalFace.Core stays persistence-ignorant.
/// </summary>
public sealed class ScreeningConfiguration : IEntityTypeConfiguration<Screening>
{
    public void Configure(EntityTypeBuilder<Screening> builder)
    {
        builder.ToTable("screenings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.LocationId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.HeartRateBpm);
        builder.Property(s => s.RespiratoryRateBpm);
        builder.Property(s => s.FatigueScore).IsRequired();
        builder.Property(s => s.IsCritical).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();

        var criticalReasonsComparer = new ValueComparer<IReadOnlyList<string>>(
            (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
            reasons => reasons.Aggregate(0, (hash, reason) => HashCode.Combine(hash, reason.GetHashCode())),
            reasons => reasons.ToList());

        builder.Property(s => s.CriticalReasons)
            .HasConversion(
                reasons => JsonSerializer.Serialize(reasons, JsonSerializerOptions.Web),
                json => DeserializeReasons(json))
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(criticalReasonsComparer);

        builder.OwnsOne(s => s.Consent, consent =>
        {
            consent.Property(c => c.SignatureHash).HasColumnName("consent_signature_hash").HasMaxLength(64);
            consent.Property(c => c.ConsentedAtUtc).HasColumnName("consent_at_utc");
        });

        builder.HasIndex(s => s.LocationId);
        builder.HasIndex(s => s.CreatedAtUtc);
    }

    private static IReadOnlyList<string> DeserializeReasons(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Web) ?? [];
}
