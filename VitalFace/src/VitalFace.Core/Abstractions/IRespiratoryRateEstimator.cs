using VitalFace.Core.Models;

namespace VitalFace.Core.Abstractions;

/// <summary>Respiratory rate estimator based on periodic vertical shoulder displacement.</summary>
public interface IRespiratoryRateEstimator
{
    /// <param name="shoulderY">Per-frame normalized vertical position of the shoulder midpoint (Pose Landmarker), one sample per frame.</param>
    /// <param name="frameRateHz">Effective sampling rate of <paramref name="shoulderY"/>.</param>
    RespiratoryEstimate Estimate(ReadOnlySpan<float> shoulderY, double frameRateHz);
}
