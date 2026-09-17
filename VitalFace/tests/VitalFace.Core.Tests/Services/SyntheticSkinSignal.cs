namespace VitalFace.Core.Tests.Services;

/// <summary>
/// Generates a synthetic forehead/cheek ROI RGB signal: a per-channel baseline mean (skin tone is
/// brightest in red, dimmest in blue, as real facial ROIs are) plus a pulsatile component at a
/// known heart rate with a different amplitude per channel — reproducing the physiological fact
/// that blood-volume absorption is wavelength-dependent, which is exactly what lets CHROM recover
/// the pulse instead of a shared lighting/motion artifact.
/// </summary>
internal static class SyntheticSkinSignal
{
    public static (float[] R, float[] G, float[] B) Generate(double bpm, double fps, double durationSeconds, int seed)
    {
        int n = (int)(fps * durationSeconds);
        var random = new Random(seed);
        var r = new float[n];
        var g = new float[n];
        var b = new float[n];

        double heartRateHz = bpm / 60.0;
        const double meanR = 150.0, meanG = 100.0, meanB = 80.0;
        const double pulseAmplitude = 2.5;

        for (int i = 0; i < n; i++)
        {
            double t = i / fps;
            double pulse = pulseAmplitude * Math.Sin(2 * Math.PI * heartRateHz * t);
            double noise = (random.NextDouble() - 0.5) * 0.3;

            r[i] = (float)(meanR + pulse * 0.6 + noise);
            g[i] = (float)(meanG + pulse * 1.0 + noise);
            b[i] = (float)(meanB + pulse * 0.4 + noise);
        }

        return (r, g, b);
    }
}
