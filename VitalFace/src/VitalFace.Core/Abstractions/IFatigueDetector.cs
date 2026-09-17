using VitalFace.Core.Models;

namespace VitalFace.Core.Abstractions;

/// <summary>PERCLOS-based blink/fatigue estimator.</summary>
public interface IFatigueDetector
{
    /// <param name="eyeOpenness">Per-frame eye openness in [0,1] (1 = fully open, 0 = fully closed), one sample per frame across the analysis window.</param>
    /// <param name="windowSeconds">Duration in seconds covered by <paramref name="eyeOpenness"/>; PERCLOS is conventionally computed over 60s.</param>
    FatigueResult CalculatePerclos(ReadOnlySpan<float> eyeOpenness, double windowSeconds);
}
