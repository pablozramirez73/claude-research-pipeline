using KineMotion.Core.Models;
using KineMotion.Core.Services;
using Xunit;

namespace KineMotion.Core.Tests.Services;

public class TremorAnalyzerTests
{
    private readonly TremorAnalyzer _sut = new();
    private const double SampleRateHz = 30.0;

    [Theory]
    [InlineData(4.5)]
    [InlineData(5.0)]
    [InlineData(8.0)]
    public void Analyze_RecoversKnownTremorFrequency_FromSyntheticSignal(double trueFrequencyHz)
    {
        var frames = SyntheticMotion.TremorHandSignal(
            frequencyHz: trueFrequencyHz, amplitude: 0.02, durationSec: 3.4, sampleRateHz: SampleRateHz);

        var result = _sut.Analyze(frames, SampleRateHz);

        // FFT bin resolution at this length is ~0.23-0.3 Hz; half a Hz of tolerance covers it comfortably.
        Assert.Equal(trueFrequencyHz, result.DominantFrequencyHz, tolerance: 0.5);
        Assert.True(result.TremorIndex > TremorResult.TremorIndexThreshold);
        Assert.True(result.IsTremorPresent);
    }

    [Fact]
    public void Analyze_FlatNearlyStillSignal_DoesNotFlagTremor()
    {
        var frames = SyntheticMotion.StillHandSignal(frameCount: 102, sampleRateHz: SampleRateHz);

        var result = _sut.Analyze(frames, SampleRateHz);

        Assert.False(result.IsTremorPresent);
    }

    [Fact]
    public void Analyze_OutsideOfPathologicalBand_IsNotCountedAsTremor()
    {
        // 1 Hz is a deliberate, voluntary oscillation (e.g. following the game's target rhythm),
        // well below the 3-12 Hz pathological tremor band — should not be flagged.
        var frames = SyntheticMotion.TremorHandSignal(
            frequencyHz: 1.0, amplitude: 0.05, durationSec: 3.4, sampleRateHz: SampleRateHz);

        var result = _sut.Analyze(frames, SampleRateHz);

        Assert.False(result.IsTremorPresent);
    }

    [Fact]
    public void Analyze_TooFewFrames_ReturnsZeroResultInsteadOfThrowing()
    {
        var frames = SyntheticMotion.TremorHandSignal(
            frequencyHz: 5.0, amplitude: 0.02, durationSec: 0.3, sampleRateHz: SampleRateHz);
        Assert.True(frames.Count < 16);

        var result = _sut.Analyze(frames, SampleRateHz);

        Assert.Equal(0, result.DominantFrequencyHz);
        Assert.Equal(0, result.AmplitudeDeg);
        Assert.Equal(0, result.TremorIndex);
        Assert.False(result.IsTremorPresent);
    }

    [Fact]
    public void Analyze_NonPositiveSampleRate_Throws()
    {
        var frames = SyntheticMotion.TremorHandSignal(5.0, 0.02, 1.0, SampleRateHz);
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Analyze(frames, 0));
    }
}
