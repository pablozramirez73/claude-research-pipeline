using VitalFace.Core.Services;

namespace VitalFace.Core.Tests.Services;

public class FatigueDetectorTests
{
    private readonly FatigueDetector _sut = new();

    [Fact]
    public void CalculatePerclos_ReturnsEmpty_WhenNoSamples()
    {
        var result = _sut.CalculatePerclos(ReadOnlySpan<float>.Empty, windowSeconds: 60);

        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.Perclos);
        Assert.False(result.IsCritical);
    }

    [Fact]
    public void CalculatePerclos_ComputesRatioOfFramesBelowClosureThreshold()
    {
        // 1000 frames: 200 "closed" (openness <= 0.2), 800 "open" -> PERCLOS = 0.20.
        var openness = new float[1000];
        for (int i = 0; i < openness.Length; i++)
            openness[i] = i < 200 ? 0.05f : 0.95f;

        var result = _sut.CalculatePerclos(openness, windowSeconds: 60);

        Assert.Equal(0.20, result.Perclos, precision: 6);
        Assert.Equal(20.0, result.Score, precision: 6);
        Assert.True(result.IsCritical); // >= 0.15 clinical threshold
    }

    [Fact]
    public void CalculatePerclos_NotCritical_WhenBelowClinicalThreshold()
    {
        // 5% closure is well under the 0.15 drowsiness red-flag.
        var openness = new float[1000];
        for (int i = 0; i < openness.Length; i++)
            openness[i] = i < 50 ? 0.05f : 0.95f;

        var result = _sut.CalculatePerclos(openness, windowSeconds: 60);

        Assert.Equal(0.05, result.Perclos, precision: 6);
        Assert.False(result.IsCritical);
    }

    [Theory]
    [InlineData(0.2f, true)]   // exactly at the closure threshold -> counted as closed
    [InlineData(0.21f, false)] // just above -> counted as open
    public void CalculatePerclos_ClosureThresholdIsInclusive(float openness, bool expectedClosed)
    {
        var samples = new[] { openness, 1.0f }; // one candidate frame + one clearly-open frame

        var result = _sut.CalculatePerclos(samples, windowSeconds: 2);

        Assert.Equal(expectedClosed ? 0.5 : 0.0, result.Perclos, precision: 6);
    }
}
