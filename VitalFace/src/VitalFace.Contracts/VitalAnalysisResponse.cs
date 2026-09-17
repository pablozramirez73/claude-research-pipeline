namespace VitalFace.Contracts;

public sealed record VitalAnalysisResponse(
    Guid ScreeningId,
    double? HeartRateBpm,
    double? RespiratoryRateBpm,
    double FatigueScore,
    bool IsCritical,
    IReadOnlyList<string> CriticalReasons,
    DateTimeOffset GeneratedAtUtc);
