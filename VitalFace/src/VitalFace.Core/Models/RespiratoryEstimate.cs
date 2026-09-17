namespace VitalFace.Core.Models;

/// <summary>Respiratory rate estimated from periodic vertical shoulder displacement (Pose Landmarker).</summary>
public readonly record struct RespiratoryEstimate
{
    public double BreathsPerMinute { get; }
    public double Confidence { get; }

    public bool IsReliable => Confidence >= 0.3;

    private RespiratoryEstimate(double breathsPerMinute, double confidence)
    {
        BreathsPerMinute = breathsPerMinute;
        Confidence = confidence;
    }

    public static RespiratoryEstimate Create(double breathsPerMinute, double confidence) =>
        new(breathsPerMinute, confidence);

    public static RespiratoryEstimate Insufficient => new(0, 0);
}
