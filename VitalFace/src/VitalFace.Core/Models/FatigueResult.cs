namespace VitalFace.Core.Models;

/// <summary>
/// PERCLOS-based fatigue estimate (Wierwille &amp; Ellsworth, 1994; adopted by FHWA drowsiness research).
/// PERCLOS is the proportion of a time window during which the eyes are at least 80% closed.
/// </summary>
public readonly record struct FatigueResult
{
    /// <summary>Fatigue score in [0,100], i.e. Perclos * 100.</summary>
    public double Score { get; }

    /// <summary>Raw PERCLOS ratio in [0,1].</summary>
    public double Perclos { get; }

    /// <summary>True when Perclos crosses the clinically-referenced drowsiness threshold (>= 0.15).</summary>
    public bool IsCritical { get; }

    public FatigueResult(double score, double perclos, bool isCritical)
    {
        Score = score;
        Perclos = perclos;
        IsCritical = isCritical;
    }

    public static FatigueResult Empty => new(0, 0, false);
}
