using VitalFace.Core.Models;

namespace VitalFace.Core.Abstractions;

/// <summary>Remote photoplethysmography heart-rate estimator (CHROM algorithm).</summary>
public interface IRppgProcessor
{
    /// <param name="r">Mean red channel value of the forehead/cheek ROI, one sample per captured frame.</param>
    /// <param name="g">Mean green channel value of the ROI.</param>
    /// <param name="b">Mean blue channel value of the ROI.</param>
    /// <param name="frameRateHz">Effective sampling rate of the buffers (camera fps, ideally jitter-corrected).</param>
    RppgEstimate EstimateHeartRate(ReadOnlySpan<float> r, ReadOnlySpan<float> g, ReadOnlySpan<float> b, double frameRateHz);
}
