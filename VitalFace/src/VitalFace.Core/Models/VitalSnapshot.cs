namespace VitalFace.Core.Models;

/// <summary>
/// Raw per-frame signal buffers captured client-side over a ~30s screening window.
/// Contains only aggregate RGB/landmark scalars — never pixel data or video frames
/// (see Sicurezza e Compliance: privacy-by-design, nothing here can reconstruct a face).
/// </summary>
public sealed record VitalSnapshot
{
    public required float[] RedChannel { get; init; }
    public required float[] GreenChannel { get; init; }
    public required float[] BlueChannel { get; init; }

    /// <summary>Per-frame eye openness in [0,1], derived as 1 - avg(eyeBlinkLeft, eyeBlinkRight) blendshapes.</summary>
    public required float[] EyeOpenness { get; init; }

    /// <summary>Per-frame normalized vertical shoulder midpoint (Pose Landmarker), used for respiration.</summary>
    public required float[] ShoulderY { get; init; }

    public required double FrameRateHz { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public void Validate()
    {
        var n = RedChannel.Length;
        if (n == 0)
            throw new ArgumentException("Snapshot contains no frames.", nameof(RedChannel));
        if (GreenChannel.Length != n || BlueChannel.Length != n)
            throw new ArgumentException("R, G, B channel buffers must be the same length.");
        if (EyeOpenness.Length != n)
            throw new ArgumentException("EyeOpenness buffer must match the RGB frame count.", nameof(EyeOpenness));
        if (ShoulderY.Length != n)
            throw new ArgumentException("ShoulderY buffer must match the RGB frame count.", nameof(ShoulderY));
        if (FrameRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(FrameRateHz), FrameRateHz, "Frame rate must be positive.");
    }
}
