using KineMotion.Core.Services;
using Xunit;

namespace KineMotion.Core.Tests.Services;

public class RomCalculatorTests
{
    private readonly RomCalculator _sut = new();

    [Fact]
    public void Calculate_RecoversPeakAndBaselineAngle_FromSyntheticSweep()
    {
        var frames = SyntheticMotion.MinimumJerkShoulderSweep(startDeg: 5, endDeg: 150, durationSec: 1.5, sampleRateHz: 30);

        var result = _sut.Calculate(frames);

        Assert.Equal(150, result.MaxAngleDeg, precision: 1);
        Assert.Equal(5, result.MinAngleDeg, precision: 1);
        Assert.Equal(145, result.RangeDeg, precision: 1);
    }

    [Fact]
    public void Calculate_SmoothMinimumJerkReach_ScoresLowerThanNoisyReach()
    {
        var smooth = SyntheticMotion.MinimumJerkShoulderSweep(startDeg: 0, endDeg: 120, durationSec: 1.2, sampleRateHz: 30);
        var noisy = SyntheticMotion.NoisyShoulderSweep(startDeg: 0, endDeg: 120, durationSec: 1.2, sampleRateHz: 30, noiseAmplitudeDeg: 8);

        var smoothResult = _sut.Calculate(smooth);
        var noisyResult = _sut.Calculate(noisy);

        Assert.True(
            smoothResult.SmoothnessScore < noisyResult.SmoothnessScore,
            $"Expected smooth reach ({smoothResult.SmoothnessScore:F3}) to score lower than noisy reach ({noisyResult.SmoothnessScore:F3}).");
    }

    [Fact]
    public void Calculate_ShortFrameSeries_ReturnsZeroSmoothnessInsteadOfThrowing()
    {
        var frames = SyntheticMotion.MinimumJerkShoulderSweep(startDeg: 0, endDeg: 90, durationSec: 0.1, sampleRateHz: 30);
        Assert.True(frames.Count < 4);

        var result = _sut.Calculate(frames);

        Assert.Equal(0, result.SmoothnessScore);
    }

    [Fact]
    public void Calculate_LargeTrunkLeanForMostOfMovement_FlagsCompensation()
    {
        var frames = SyntheticMotion.ConstantTrunkLean(leanDeg: 25, frameCount: 60, sampleRateHz: 30);

        var result = _sut.Calculate(frames);

        Assert.True(result.CompensationDetected);
        Assert.Equal(1.0, result.CompensationRatio);
    }

    [Fact]
    public void Calculate_SmallTrunkLean_DoesNotFlagCompensation()
    {
        var frames = SyntheticMotion.ConstantTrunkLean(leanDeg: 5, frameCount: 60, sampleRateHz: 30);

        var result = _sut.Calculate(frames);

        Assert.False(result.CompensationDetected);
        Assert.Equal(0.0, result.CompensationRatio);
    }

    [Fact]
    public void Calculate_EmptyFrameSeries_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.Calculate([]));
    }
}
