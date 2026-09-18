namespace KineMotion.Core.Models;

/// <param name="MaxAngleDeg">Peak joint angle reached during the session — the clinical ROM figure.</param>
/// <param name="MinAngleDeg">Baseline/rest angle, useful to confirm the patient returned to start each rep.</param>
/// <param name="SmoothnessScore">
/// Dimensionless (log) normalized-jerk score (Hogan &amp; Sternad, 2009). Lower is smoother;
/// a value near 0 is a clean bell-shaped velocity profile, values above ~6-7 typically indicate
/// tremor, hesitation, or multi-peaked (segmented) movement.
/// </param>
/// <param name="CompensationDetected">True when trunk lean stayed outside <see cref="CompensationThresholdDeg"/> for too large a share of the movement.</param>
/// <param name="CompensationRatio">Fraction of frames where trunk lean exceeded <see cref="CompensationThresholdDeg"/>.</param>
public sealed record RomResult(
    double MaxAngleDeg,
    double MinAngleDeg,
    double SmoothnessScore,
    bool CompensationDetected,
    double CompensationRatio)
{
    public const double CompensationThresholdDeg = 15.0;
    public const double CompensationRatioThreshold = 0.20;

    public double RangeDeg => MaxAngleDeg - MinAngleDeg;
}
