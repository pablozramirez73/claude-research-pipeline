using VitalFace.Core.Services;

namespace VitalFace.Core.Tests.Services;

public class RespiratoryRateEstimatorTests
{
    private readonly RespiratoryRateEstimator _sut = new();

    [Theory]
    [InlineData(12.0)]
    [InlineData(16.0)]
    [InlineData(24.0)]
    public void Estimate_RecoversKnownBreathingRateFromSyntheticShoulderMovement(double expectedBreathsPerMinute)
    {
        const double fps = 30.0;
        const double durationSeconds = 40.0;
        int n = (int)(fps * durationSeconds);
        var shoulderY = new float[n];
        double breathingHz = expectedBreathsPerMinute / 60.0;
        var random = new Random(7);

        for (int i = 0; i < n; i++)
        {
            double t = i / fps;
            double displacement = 0.02 * Math.Sin(2 * Math.PI * breathingHz * t);
            shoulderY[i] = (float)(0.5 + displacement + (random.NextDouble() - 0.5) * 0.002);
        }

        var estimate = _sut.Estimate(shoulderY, fps);

        Assert.True(estimate.IsReliable, $"Expected a reliable estimate (confidence={estimate.Confidence:F2}).");
        Assert.InRange(estimate.BreathsPerMinute, expectedBreathsPerMinute - 1.5, expectedBreathsPerMinute + 1.5);
    }

    [Fact]
    public void Estimate_ReturnsInsufficient_WhenWindowTooShortForRespiratoryFrequencies()
    {
        var shoulderY = new float[150]; // 5s @ 30fps, below the 15s minimum

        var estimate = _sut.Estimate(shoulderY, frameRateHz: 30.0);

        Assert.False(estimate.IsReliable);
        Assert.Equal(0, estimate.BreathsPerMinute);
    }
}
