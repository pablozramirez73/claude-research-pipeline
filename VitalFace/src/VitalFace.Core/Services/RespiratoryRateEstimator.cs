using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;
using VitalFace.Core.Services.SignalProcessing;

namespace VitalFace.Core.Services;

/// <summary>
/// Estimates respiratory rate from the periodic vertical oscillation of the shoulder midpoint
/// (MediaPipe Pose Landmarker, lite model) as the chest rises and falls during breathing. Reuses
/// the same detrend -&gt; window -&gt; FFT pipeline as <see cref="RppgProcessor"/>, tuned to the
/// much lower respiratory frequency band.
/// </summary>
public sealed class RespiratoryRateEstimator : IRespiratoryRateEstimator
{
    private const double MinRespiratoryHz = 0.13; // 8 breaths/min
    private const double MaxRespiratoryHz = 0.70; // 42 breaths/min (covers tachypnea)
    private const double MinWindowSeconds = 15.0; // need several breath cycles to resolve ~0.2 Hz

    public RespiratoryEstimate Estimate(ReadOnlySpan<float> shoulderY, double frameRateHz)
    {
        if (frameRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRateHz));

        double windowSeconds = shoulderY.Length / frameRateHz;
        if (shoulderY.Length == 0 || windowSeconds < MinWindowSeconds)
            return RespiratoryEstimate.Insufficient;

        var signal = new double[shoulderY.Length];
        for (int i = 0; i < shoulderY.Length; i++)
            signal[i] = shoulderY[i];

        DigitalSignalProcessing.RemoveLinearTrend(signal);
        DigitalSignalProcessing.ApplyHannWindow(signal);

        var spectrum = DigitalSignalProcessing.ForwardFft(signal);
        var (peakHz, confidence) = DigitalSignalProcessing.FindDominantFrequency(spectrum, frameRateHz, MinRespiratoryHz, MaxRespiratoryHz);

        return RespiratoryEstimate.Create(peakHz * 60.0, confidence);
    }
}
