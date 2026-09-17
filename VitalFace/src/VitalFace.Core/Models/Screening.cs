namespace VitalFace.Core.Models;

/// <summary>
/// Persistence-ignorant aggregate root for a single kiosk screening. Lives in Core (not
/// Infrastructure) per Clean Architecture: the domain model must not depend on EF Core.
/// <see cref="VitalFace.Infrastructure"/> maps this via a Fluent API configuration rather than
/// annotating the entity itself.
/// </summary>
public sealed class Screening
{
    public Guid Id { get; private set; }
    public required string LocationId { get; init; }
    public double? HeartRateBpm { get; private set; }
    public double? RespiratoryRateBpm { get; private set; }
    public double FatigueScore { get; private set; }
    public bool IsCritical { get; private set; }
    public IReadOnlyList<string> CriticalReasons { get; private set; } = [];
    public ConsentRecord? Consent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Screening() { } // EF Core

    public static Screening Create(string locationId, VitalResult result, ConsentRecord consent)
    {
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("A screening must be attributed to a location (e.g. farmacia X).", nameof(locationId));

        return new Screening
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            HeartRateBpm = result.HeartRateBpm,
            RespiratoryRateBpm = result.RespiratoryRateBpm,
            FatigueScore = result.FatigueScore,
            IsCritical = result.IsCritical,
            CriticalReasons = result.CriticalReasons,
            Consent = consent,
            CreatedAtUtc = result.GeneratedAtUtc
        };
    }
}
