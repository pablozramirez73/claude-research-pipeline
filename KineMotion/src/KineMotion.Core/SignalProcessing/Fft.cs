using System.Numerics;

namespace KineMotion.Core.SignalProcessing;

/// <summary>
/// Minimal DSP primitives shared by Core's frequency-domain analyzers (currently
/// <see cref="Services.TremorAnalyzer"/>). Deliberately dependency-free: a radix-2 Cooley-Tukey
/// FFT is a couple dozen lines and pulling in a native FFT package for one call site is not
/// worth the added deployment surface (and blocks trimming/AOT scenarios later).
/// </summary>
public static class Fft
{
    /// <summary>Removes the linear best-fit trend, then applies a Hann window in place.</summary>
    public static double[] DetrendAndWindow(ReadOnlySpan<double> signal)
    {
        int n = signal.Length;
        var result = new double[n];

        double meanX = (n - 1) / 2.0;
        double meanY = 0;
        for (int i = 0; i < n; i++) meanY += signal[i];
        meanY /= n;

        double num = 0, den = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = i - meanX;
            num += dx * (signal[i] - meanY);
            den += dx * dx;
        }
        double slope = den > 0 ? num / den : 0;
        double intercept = meanY - slope * meanX;

        for (int i = 0; i < n; i++)
        {
            double detrended = signal[i] - (slope * i + intercept);
            double window = n > 1 ? 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1))) : 1.0;
            result[i] = detrended * window;
        }

        return result;
    }

    /// <summary>
    /// Radix-2 Cooley-Tukey FFT. <paramref name="signal"/> is zero-padded up to the next power of
    /// two; the caller gets the padded length back so it can compute correct frequency bins.
    /// </summary>
    public static Complex[] Forward(ReadOnlySpan<double> signal)
    {
        int n = NextPowerOfTwo(Math.Max(1, signal.Length));
        var buffer = new Complex[n];
        for (int i = 0; i < signal.Length; i++) buffer[i] = new Complex(signal[i], 0);
        // remaining entries default to Complex.Zero (zero-padding)

        FftInPlace(buffer);
        return buffer;
    }

    /// <summary>Bin index -&gt; frequency in Hz for an FFT of length <paramref name="fftLength"/> sampled at <paramref name="sampleRateHz"/>.</summary>
    public static double BinToHz(int bin, int fftLength, double sampleRateHz) => bin * sampleRateHz / fftLength;

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    private static void FftInPlace(Complex[] a)
    {
        int n = a.Length;
        if (n <= 1) return;

        // Bit-reversal permutation.
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = a[i + j];
                    Complex v = a[i + j + len / 2] * w;
                    a[i + j] = u + v;
                    a[i + j + len / 2] = u - v;
                    w *= wLen;
                }
            }
        }
    }
}
