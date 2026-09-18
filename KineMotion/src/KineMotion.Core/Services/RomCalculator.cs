using KineMotion.Core.Models;
using KineMotion.Core.ValueObjects;

namespace KineMotion.Core.Services;

/// <summary>
/// Computes range-of-motion, movement smoothness and trunk-compensation metrics from a session's
/// frame series. Pure function of its input: no I/O, no framework dependency, fully unit-testable
/// with synthetic <see cref="PoseFrame"/> sequences.
/// </summary>
public sealed class RomCalculator
{
    /// <param name="frames">Chronologically ordered frames for one rep or one whole session.</param>
    /// <param name="joint">Which angle series to analyze (defaults to shoulder abduction, the primary signal for the ShoulderBirdFlight game).</param>
    public RomResult Calculate(IReadOnlyList<PoseFrame> frames, JointAngle joint = JointAngle.ShoulderAbduction)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("Cannot compute ROM from an empty frame series.", nameof(frames));
        }

        double maxAngle = double.MinValue;
        double minAngle = double.MaxValue;
        foreach (var frame in frames)
        {
            double angle = frame.AngleDeg(joint);
            if (angle > maxAngle) maxAngle = angle;
            if (angle < minAngle) minAngle = angle;
        }

        double smoothness = CalculateNormalizedJerk(frames, joint, maxAngle - minAngle);
        var (compensationDetected, compensationRatio) = DetectTrunkCompensation(frames);

        return new RomResult(maxAngle, minAngle, smoothness, compensationDetected, compensationRatio);
    }

    /// <summary>
    /// Dimensionless-jerk smoothness score (Hogan &amp; Sternad, 2009), log-compressed so it stays
    /// readable across very different movement durations/amplitudes. Needs at least 4 frames to
    /// take a third derivative; shorter series score as perfectly smooth (0) rather than throwing,
    /// since a 2-3 frame rep is too short to say anything meaningful about its shape.
    /// </summary>
    private static double CalculateNormalizedJerk(IReadOnlyList<PoseFrame> frames, JointAngle joint, double amplitudeDeg)
    {
        if (frames.Count < 4 || amplitudeDeg <= 0)
        {
            return 0.0;
        }

        int n = frames.Count;
        var angle = new double[n];
        var timeSec = new double[n];
        for (int i = 0; i < n; i++)
        {
            angle[i] = frames[i].AngleDeg(joint);
            timeSec[i] = frames[i].TimestampMs / 1000.0;
        }

        double durationSec = timeSec[^1] - timeSec[0];
        if (durationSec <= 0)
        {
            return 0.0;
        }

        double[] velocity = CentralDifference(angle, timeSec);
        double[] acceleration = CentralDifference(velocity, timeSec);
        double[] jerk = CentralDifference(acceleration, timeSec);

        double jerkIntegral = 0;
        for (int i = 1; i < n; i++)
        {
            double dt = timeSec[i] - timeSec[i - 1];
            // Trapezoid rule on jerk^2.
            jerkIntegral += 0.5 * (jerk[i] * jerk[i] + jerk[i - 1] * jerk[i - 1]) * dt;
        }

        double normalizedJerk = Math.Sqrt(0.5 * jerkIntegral * Math.Pow(durationSec, 5)) / amplitudeDeg;
        return Math.Log(1 + normalizedJerk);
    }

    private static double[] CentralDifference(double[] values, double[] timeSec)
    {
        int n = values.Length;
        var derivative = new double[n];
        for (int i = 0; i < n; i++)
        {
            if (i == 0)
            {
                double dt = timeSec[1] - timeSec[0];
                derivative[i] = dt > 0 ? (values[1] - values[0]) / dt : 0;
            }
            else if (i == n - 1)
            {
                double dt = timeSec[i] - timeSec[i - 1];
                derivative[i] = dt > 0 ? (values[i] - values[i - 1]) / dt : 0;
            }
            else
            {
                double dt = timeSec[i + 1] - timeSec[i - 1];
                derivative[i] = dt > 0 ? (values[i + 1] - values[i - 1]) / dt : 0;
            }
        }
        return derivative;
    }

    private static (bool Detected, double Ratio) DetectTrunkCompensation(IReadOnlyList<PoseFrame> frames)
    {
        int overThreshold = 0;
        foreach (var frame in frames)
        {
            if (Math.Abs(frame.TrunkLeanDeg) > RomResult.CompensationThresholdDeg)
            {
                overThreshold++;
            }
        }

        double ratio = (double)overThreshold / frames.Count;
        return (ratio > RomResult.CompensationRatioThreshold, ratio);
    }
}
