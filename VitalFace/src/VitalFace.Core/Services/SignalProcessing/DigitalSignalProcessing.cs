using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace VitalFace.Core.Services.SignalProcessing;

/// <summary>
/// Small, dependency-light DSP helpers shared by the rPPG and respiration estimators. Not a
/// general-purpose signal processing library — only what spectral estimation over short
/// (10-60s) physiological windows needs: detrend, window, FFT, dominant-frequency search.
/// </summary>
public static class DigitalSignalProcessing
{
    /// <summary>Removes the best-fit linear trend (least squares) in place, eliminating slow illumination/motion drift.</summary>
    public static void RemoveLinearTrend(Span<double> signal)
    {
        int n = signal.Length;
        if (n < 2) return;

        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += signal[i];
            sumXY += i * signal[i];
            sumXX += (double)i * i;
        }

        double denominator = n * sumXX - sumX * sumX;
        if (Math.Abs(denominator) < 1e-12) return;

        double slope = (n * sumXY - sumX * sumY) / denominator;
        double intercept = (sumY - slope * sumX) / n;

        for (int i = 0; i < n; i++)
            signal[i] -= slope * i + intercept;
    }

    /// <summary>Applies a Hann window in place to reduce spectral leakage before FFT.</summary>
    public static void ApplyHannWindow(Span<double> signal)
    {
        int n = signal.Length;
        if (n < 2) return;
        for (int i = 0; i < n; i++)
            signal[i] *= 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
    }

    /// <summary>Zero-pads to the next power of two and runs a forward FFT (MathNet.Numerics, radix-2).</summary>
    public static Complex[] ForwardFft(ReadOnlySpan<double> signal)
    {
        int paddedLength = NextPowerOfTwo(Math.Max(signal.Length, 2));
        var samples = new Complex[paddedLength];
        for (int i = 0; i < signal.Length; i++)
            samples[i] = new Complex(signal[i], 0);
        // Remaining entries stay Complex.Zero — this is the zero-padding.

        Fourier.Forward(samples);
        return samples;
    }

    /// <summary>
    /// Finds the dominant frequency within [minHz, maxHz] and a confidence score
    /// (peak power / total in-band power — a fast proxy for spectral purity, low when the
    /// subject moved too much for a clean periodic component to dominate).
    /// </summary>
    public static (double FrequencyHz, double Confidence) FindDominantFrequency(
        Complex[] spectrum, double frameRateHz, double minHz, double maxHz)
    {
        int n = spectrum.Length;
        double binHz = frameRateHz / n;

        int minBin = Math.Max(1, (int)Math.Floor(minHz / binHz));
        int maxBin = Math.Min(n / 2 - 1, (int)Math.Ceiling(maxHz / binHz));

        if (minBin > maxBin)
            return (0, 0);

        int peakBin = minBin;
        double peakPower = 0;
        double totalPower = 0;

        for (int bin = minBin; bin <= maxBin; bin++)
        {
            double power = spectrum[bin].Magnitude * spectrum[bin].Magnitude;
            totalPower += power;
            if (power > peakPower)
            {
                peakPower = power;
                peakBin = bin;
            }
        }

        // A windowed signal's energy spreads across the window's mainlobe (a handful of adjacent
        // bins), not a single bin, so confidence is the power captured by a small neighborhood
        // around the peak rather than the single peak bin alone.
        const int neighborhoodRadius = 2;
        int neighborhoodLo = Math.Max(minBin, peakBin - neighborhoodRadius);
        int neighborhoodHi = Math.Min(maxBin, peakBin + neighborhoodRadius);
        double neighborhoodPower = 0;
        for (int bin = neighborhoodLo; bin <= neighborhoodHi; bin++)
            neighborhoodPower += spectrum[bin].Magnitude * spectrum[bin].Magnitude;

        double confidence = totalPower > 1e-12 ? neighborhoodPower / totalPower : 0;
        return (peakBin * binHz, confidence);
    }

    private static int NextPowerOfTwo(int value)
    {
        int power = 1;
        while (power < value) power <<= 1;
        return power;
    }
}
