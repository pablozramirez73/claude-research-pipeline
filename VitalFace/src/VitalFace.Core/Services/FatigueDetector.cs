using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;

namespace VitalFace.Core.Services;

/// <summary>
/// PERCLOS ("PERcentage of eyelid CLOSure") fatigue estimator. An eye counts as "closed" when its
/// openness (1 - MediaPipe eyeBlinkLeft/Right blendshape) drops below <see cref="ClosureThreshold"/>,
/// i.e. the eyelid covers at least ~80% of the eye — the definition used by Wierwille &amp;
/// Ellsworth (1994) and later adopted in FHWA drowsy-driving research.
/// </summary>
public sealed class FatigueDetector : IFatigueDetector
{
    private const float ClosureThreshold = 0.2f; // openness <= 0.2  <=>  eye is >=80% closed

    public FatigueResult CalculatePerclos(ReadOnlySpan<float> eyeOpenness, double windowSeconds)
    {
        if (eyeOpenness.Length == 0)
            return FatigueResult.Empty;

        int closedFrames = 0;
        foreach (var openness in eyeOpenness)
        {
            if (openness <= ClosureThreshold)
                closedFrames++;
        }

        double perclos = (double)closedFrames / eyeOpenness.Length;
        double score = Math.Clamp(perclos * 100.0, 0, 100);
        bool isCritical = perclos >= ClinicalThresholds.PerclosCriticalThreshold;

        return new FatigueResult(score, perclos, isCritical);
    }
}
