using System.Numerics;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;
using VitalFace.Core.Services.SignalProcessing;

namespace VitalFace.Core.Services;

/// <summary>
/// C# implementation of the CHROM algorithm (de Haan &amp; Jeanne, "Robust Pulse Rate From
/// Chrominance-Based rPPG", IEEE Trans. Biomed. Eng., 2013). CHROM projects the RGB signal onto a
/// chrominance subspace that cancels the specular reflection / motion component shared by all
/// three channels, which makes it substantially more robust to head motion and ambient lighting
/// changes than tracking the raw green channel alone.
/// </summary>
public sealed class RppgProcessor : IRppgProcessor
{
    private const double MinHeartRateHz = 0.7; // 42 bpm
    private const double MaxHeartRateHz = 4.0; // 240 bpm
    private const double MinWindowSeconds = 8.0; // shorter windows don't resolve low heart rates well

    public RppgEstimate EstimateHeartRate(ReadOnlySpan<float> r, ReadOnlySpan<float> g, ReadOnlySpan<float> b, double frameRateHz)
    {
        if (r.Length != g.Length || g.Length != b.Length)
            throw new ArgumentException("R, G and B channel buffers must have the same length.");
        if (frameRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRateHz));

        double windowSeconds = r.Length / frameRateHz;
        if (r.Length == 0 || windowSeconds < MinWindowSeconds)
            return RppgEstimate.Insufficient(windowSeconds);

        var chrominance = ProjectChrom(r, g, b);

        DigitalSignalProcessing.RemoveLinearTrend(chrominance);
        DigitalSignalProcessing.ApplyHannWindow(chrominance);

        var spectrum = DigitalSignalProcessing.ForwardFft(chrominance);
        var (peakHz, confidence) = DigitalSignalProcessing.FindDominantFrequency(spectrum, frameRateHz, MinHeartRateHz, MaxHeartRateHz);

        return RppgEstimate.Create(peakHz * 60.0, confidence, windowSeconds);
    }

    /// <summary>
    /// Projects normalized RGB onto the CHROM chrominance signal S = X - alpha*Y, where
    /// X = 3*R_n - 2*G_n and Y = 1.5*R_n + G_n - 1.5*B_n (R_n/G_n/B_n are each channel divided by
    /// its own temporal mean), and alpha = std(X)/std(Y) balances the two projections so the
    /// specular/motion component cancels between them.
    /// </summary>
    private static double[] ProjectChrom(ReadOnlySpan<float> r, ReadOnlySpan<float> g, ReadOnlySpan<float> b)
    {
        int n = r.Length;
        double meanR = Mean(r), meanG = Mean(g), meanB = Mean(b);

        var x = new double[n];
        var y = new double[n];

        for (int i = 0; i < n; i++)
        {
            double rn = meanR > 1e-6 ? r[i] / meanR : 0;
            double gn = meanG > 1e-6 ? g[i] / meanG : 0;
            double bn = meanB > 1e-6 ? b[i] / meanB : 0;

            x[i] = 3 * rn - 2 * gn;
            y[i] = 1.5 * rn + gn - 1.5 * bn;
        }

        double stdX = StdDev(x);
        double stdY = StdDev(y);
        double alpha = stdY > 1e-9 ? stdX / stdY : 1.0;

        var s = new double[n];
        for (int i = 0; i < n; i++)
            s[i] = x[i] - alpha * y[i];

        return s;
    }

    /// <summary>SIMD-accelerated mean over the (potentially large, 30s @ 30fps ~= 900 sample) ROI buffer.</summary>
    private static double Mean(ReadOnlySpan<float> values)
    {
        if (values.Length == 0) return 0;

        int i = 0;
        int simdWidth = Vector<float>.Count;
        float simdSum = 0;

        if (values.Length >= simdWidth)
        {
            var accumulator = Vector<float>.Zero;
            for (; i <= values.Length - simdWidth; i += simdWidth)
                accumulator += new Vector<float>(values.Slice(i, simdWidth));
            simdSum = Vector.Sum(accumulator);
        }

        double sum = simdSum;
        for (; i < values.Length; i++)
            sum += values[i];

        return sum / values.Length;
    }

    private static double StdDev(ReadOnlySpan<double> values)
    {
        if (values.Length == 0) return 0;

        double mean = 0;
        foreach (var value in values) mean += value;
        mean /= values.Length;

        double sumSquaredDeviation = 0;
        foreach (var value in values) sumSquaredDeviation += (value - mean) * (value - mean);

        return Math.Sqrt(sumSquaredDeviation / values.Length);
    }
}
