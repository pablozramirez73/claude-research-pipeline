using VitalFace.Core.Services;

namespace VitalFace.Core.Tests.Services;

public class RppgProcessorTests
{
    private readonly RppgProcessor _sut = new();

    [Theory]
    [InlineData(60.0)]
    [InlineData(75.0)]
    [InlineData(100.0)]
    public void EstimateHeartRate_RecoversKnownPulseFrequencyFromSyntheticRoiSignal(double expectedBpm)
    {
        const double fps = 30.0;
        const double durationSeconds = 20.0;
        var (r, g, b) = SyntheticSkinSignal.Generate(expectedBpm, fps, durationSeconds, seed: 42);

        var estimate = _sut.EstimateHeartRate(r, g, b, fps);

        Assert.True(estimate.IsReliable, $"Expected a reliable estimate (confidence={estimate.Confidence:F2}, window={estimate.WindowSeconds:F1}s).");
        // FFT bin resolution over a 20s @ 30fps window (zero-padded to 1024) is ~1.76 bpm.
        Assert.InRange(estimate.Bpm, expectedBpm - 3.0, expectedBpm + 3.0);
    }

    [Fact]
    public void EstimateHeartRate_ReturnsInsufficient_WhenWindowShorterThanMinimum()
    {
        const double fps = 30.0;
        var (r, g, b) = SyntheticSkinSignal.Generate(bpm: 70, fps, durationSeconds: 3.0, seed: 1);

        var estimate = _sut.EstimateHeartRate(r, g, b, fps);

        Assert.False(estimate.IsReliable);
        Assert.Equal(0, estimate.Bpm);
        Assert.Equal(0, estimate.Confidence);
    }

    [Fact]
    public void EstimateHeartRate_ThrowsOnMismatchedChannelLengths()
    {
        var r = new float[100];
        var g = new float[90];
        var b = new float[100];

        Assert.Throws<ArgumentException>(() => _sut.EstimateHeartRate(r, g, b, 30.0));
    }

    [Fact]
    public void EstimateHeartRate_IsRobustToPureCommonModeMotionArtifact()
    {
        // A large in-phase, equal-amplitude swing across R/G/B (e.g. a lighting flicker or gross
        // camera shake) has no periodic component once normalized, so CHROM should NOT report a
        // strong, reliable "pulse" at the artifact's frequency.
        const double fps = 30.0;
        const double durationSeconds = 20.0;
        int n = (int)(fps * durationSeconds);
        var r = new float[n];
        var g = new float[n];
        var b = new float[n];
        const double artifactHz = 2.0; // 120 bpm-equivalent motion artifact

        for (int i = 0; i < n; i++)
        {
            double t = i / fps;
            float commonMode = (float)(20.0 * Math.Sin(2 * Math.PI * artifactHz * t));
            r[i] = 150f + commonMode;
            g[i] = 150f + commonMode;
            b[i] = 150f + commonMode;
        }

        var estimate = _sut.EstimateHeartRate(r, g, b, fps);

        // Equal means + equal in-phase swings collapse X and Y to (near) zero, so confidence
        // should be low rather than confidently locking onto the artifact frequency.
        Assert.True(estimate.Confidence < 0.5, $"Expected low confidence for a pure common-mode artifact, got {estimate.Confidence:F2}.");
    }
}
