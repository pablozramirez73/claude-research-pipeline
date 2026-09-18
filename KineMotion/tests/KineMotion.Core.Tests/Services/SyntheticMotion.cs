using KineMotion.Core.ValueObjects;

namespace KineMotion.Core.Tests.Services;

/// <summary>Synthetic <see cref="PoseFrame"/> generators shared across Core algorithm tests.</summary>
internal static class SyntheticMotion
{
    /// <summary>
    /// Minimum-jerk point-to-point trajectory (5th-order polynomial, Flash &amp; Hogan 1985): the
    /// textbook example of a maximally smooth reach, zero velocity/acceleration at both ends.
    /// </summary>
    public static IReadOnlyList<PoseFrame> MinimumJerkShoulderSweep(
        double startDeg, double endDeg, double durationSec, double sampleRateHz)
    {
        int sampleCount = (int)(durationSec * sampleRateHz);
        var frames = new List<PoseFrame>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / sampleRateHz;
            double tau = Math.Clamp(t / durationSec, 0, 1);
            double shape = 10 * Math.Pow(tau, 3) - 15 * Math.Pow(tau, 4) + 6 * Math.Pow(tau, 5);
            double angle = startDeg + (endDeg - startDeg) * shape;
            frames.Add(new PoseFrame(t * 1000.0, angle, 0, 0, 0, 0));
        }
        return frames;
    }

    /// <summary>Same endpoints/duration as <see cref="MinimumJerkShoulderSweep"/>, with high-frequency noise layered on top to simulate a tremulous/hesitant reach.</summary>
    public static IReadOnlyList<PoseFrame> NoisyShoulderSweep(
        double startDeg, double endDeg, double durationSec, double sampleRateHz, double noiseAmplitudeDeg, int seed = 42)
    {
        var random = new Random(seed);
        int sampleCount = (int)(durationSec * sampleRateHz);
        var frames = new List<PoseFrame>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / sampleRateHz;
            double tau = Math.Clamp(t / durationSec, 0, 1);
            double shape = 10 * Math.Pow(tau, 3) - 15 * Math.Pow(tau, 4) + 6 * Math.Pow(tau, 5);
            double baseAngle = startDeg + (endDeg - startDeg) * shape;
            double noise = noiseAmplitudeDeg * (random.NextDouble() * 2 - 1);
            frames.Add(new PoseFrame(t * 1000.0, baseAngle + noise, 0, 0, 0, 0));
        }
        return frames;
    }

    public static IReadOnlyList<PoseFrame> ConstantTrunkLean(double leanDeg, int frameCount, double sampleRateHz)
    {
        var frames = new List<PoseFrame>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            frames.Add(new PoseFrame(i * 1000.0 / sampleRateHz, 45, 0, leanDeg, 0, 0));
        }
        return frames;
    }

    /// <summary>Wrist position oscillating at <paramref name="frequencyHz"/> plus a slow drift, as a synthetic tremor signal.</summary>
    public static IReadOnlyList<PoseFrame> TremorHandSignal(
        double frequencyHz, double amplitude, double durationSec, double sampleRateHz, double driftPerSec = 0.02)
    {
        int sampleCount = (int)(durationSec * sampleRateHz);
        var frames = new List<PoseFrame>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / sampleRateHz;
            double x = amplitude * Math.Sin(2 * Math.PI * frequencyHz * t) + driftPerSec * t;
            frames.Add(new PoseFrame(t * 1000.0, 0, 0, 0, x, 0));
        }
        return frames;
    }

    public static IReadOnlyList<PoseFrame> StillHandSignal(int frameCount, double sampleRateHz, double seed = 7)
    {
        var random = new Random((int)seed);
        var frames = new List<PoseFrame>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            // A little sensor noise, but nothing resembling a coherent oscillation.
            double x = 0.0005 * (random.NextDouble() * 2 - 1);
            frames.Add(new PoseFrame(i * 1000.0 / sampleRateHz, 0, 0, 0, x, 0));
        }
        return frames;
    }
}
