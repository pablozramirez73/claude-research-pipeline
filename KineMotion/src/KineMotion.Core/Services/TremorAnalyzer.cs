using KineMotion.Core.Models;
using KineMotion.Core.SignalProcessing;
using KineMotion.Core.ValueObjects;

namespace KineMotion.Core.Services;

/// <summary>
/// FFT-based hand-tremor detector, used by MirrorHand and HandGrip sessions where the clinically
/// relevant signal is oscillation of the wrist landmark rather than a joint angle. Requires a
/// (close to) uniformly sampled frame series — MediaPipe's VIDEO running mode delivers frames at
/// a fixed target rate, so this holds in practice; wildly variable frame spacing would need
/// resampling first, which is out of scope for this MVP.
/// </summary>
public sealed class TremorAnalyzer
{
    private const int MinFramesForAnalysis = 16;

    /// <param name="frames">Chronologically ordered frames; <see cref="PoseFrame.HandX"/>/<see cref="PoseFrame.HandY"/> carry the wrist signal.</param>
    /// <param name="sampleRateHz">Effective capture rate. MediaPipe pose-game-bridge.js reports this alongside the frame batch since it can drift from the nominal 30fps under load.</param>
    public TremorResult Analyze(IReadOnlyList<PoseFrame> frames, double sampleRateHz)
    {
        if (sampleRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be positive.");
        }

        if (frames.Count < MinFramesForAnalysis)
        {
            return new TremorResult(0, 0, 0);
        }

        var signal = new double[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            // Planar displacement magnitude of the wrist from the session mean, so tremor along
            // either axis (or a mix of both) is captured by a single scalar series.
            signal[i] = Math.Sqrt(frames[i].HandX * frames[i].HandX + frames[i].HandY * frames[i].HandY);
        }

        double[] prepared = Fft.DetrendAndWindow(signal);
        var spectrum = Fft.Forward(prepared);
        int fftLength = spectrum.Length;
        int usableBins = fftLength / 2; // real signal: spectrum is conjugate-symmetric.

        double totalPower = 0;
        double bandPower = 0;
        double bandPeakAmplitude = 0;
        double bandPeakFrequency = 0;

        for (int bin = 1; bin < usableBins; bin++) // skip DC (bin 0)
        {
            double frequency = Fft.BinToHz(bin, fftLength, sampleRateHz);
            double magnitude = spectrum[bin].Magnitude;
            double power = magnitude * magnitude;
            totalPower += power;

            if (frequency >= TremorResult.TremorBandLowHz && frequency <= TremorResult.TremorBandHighHz)
            {
                bandPower += power;
                // Amplitude of a single sinusoidal component from its FFT bin magnitude.
                double amplitude = 2.0 * magnitude / frames.Count;
                if (amplitude > bandPeakAmplitude)
                {
                    bandPeakAmplitude = amplitude;
                    bandPeakFrequency = frequency;
                }
            }
        }

        double tremorIndex = totalPower > 0 ? bandPower / totalPower : 0;
        return new TremorResult(bandPeakFrequency, bandPeakAmplitude, tremorIndex);
    }
}
