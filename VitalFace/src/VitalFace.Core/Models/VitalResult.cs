namespace VitalFace.Core.Models;

/// <summary>Aggregate pre-triage outcome returned by the API and persisted as a <see cref="Screening"/>.</summary>
public sealed record VitalResult
{
    public double? HeartRateBpm { get; init; }
    public double? RespiratoryRateBpm { get; init; }
    public required double FatigueScore { get; init; }
    public required bool IsCritical { get; init; }
    public required IReadOnlyList<string> CriticalReasons { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>
    /// Domain rule combining the three independent measurements into a single triage flag.
    /// An estimate below its reliability threshold (see <see cref="RppgEstimate.IsReliable"/> /
    /// <see cref="RespiratoryEstimate.IsReliable"/>) is reported as null rather than a misleading number.
    /// </summary>
    public static VitalResult Evaluate(RppgEstimate heartRate, RespiratoryEstimate respiratoryRate, FatigueResult fatigue, DateTimeOffset generatedAtUtc)
    {
        double? hrBpm = heartRate.IsReliable ? heartRate.Bpm : null;
        double? rrBpm = respiratoryRate.IsReliable ? respiratoryRate.BreathsPerMinute : null;

        var reasons = new List<string>();
        if (hrBpm is double hr)
        {
            if (hr < ClinicalThresholds.BradycardiaBpm)
                reasons.Add($"Frequenza cardiaca bassa ({hr:F0} bpm)");
            else if (hr > ClinicalThresholds.TachycardiaBpm)
                reasons.Add($"Frequenza cardiaca elevata ({hr:F0} bpm)");
        }

        if (rrBpm is double rr)
        {
            if (rr < ClinicalThresholds.BradypneaBpm)
                reasons.Add($"Frequenza respiratoria bassa ({rr:F0} atti/min)");
            else if (rr > ClinicalThresholds.TachypneaBpm)
                reasons.Add($"Frequenza respiratoria elevata ({rr:F0} atti/min)");
        }

        if (fatigue.IsCritical)
            reasons.Add($"Indice di affaticamento elevato (PERCLOS {fatigue.Perclos:P0})");

        return new VitalResult
        {
            HeartRateBpm = hrBpm,
            RespiratoryRateBpm = rrBpm,
            FatigueScore = fatigue.Score,
            IsCritical = reasons.Count > 0,
            CriticalReasons = reasons,
            GeneratedAtUtc = generatedAtUtc
        };
    }
}
