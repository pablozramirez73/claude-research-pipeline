namespace VitalFace.Core.Models;

/// <summary>Result of the CHROM rPPG heart-rate estimation over one signal window.</summary>
public readonly record struct RppgEstimate
{
    public double Bpm { get; }

    /// <summary>Ratio of spectral power at the peak vs. total power in the physiological band, in [0,1]. Low confidence usually means excessive motion.</summary>
    public double Confidence { get; }

    public double WindowSeconds { get; }

    public bool IsReliable => Confidence >= 0.35 && WindowSeconds >= 8.0;

    private RppgEstimate(double bpm, double confidence, double windowSeconds)
    {
        Bpm = bpm;
        Confidence = confidence;
        WindowSeconds = windowSeconds;
    }

    public static RppgEstimate Create(double bpm, double confidence, double windowSeconds) =>
        new(bpm, confidence, windowSeconds);

    public static RppgEstimate Insufficient(double windowSeconds) => new(0, 0, windowSeconds);
}
