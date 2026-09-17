using VitalFace.Core.Models;

namespace VitalFace.Core.Tests.Models;

public class VitalResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_FlagsCritical_WhenHeartRateIsTachycardic()
    {
        var heartRate = RppgEstimate.Create(bpm: 135, confidence: 0.8, windowSeconds: 20);
        var respiratory = RespiratoryEstimate.Create(breathsPerMinute: 16, confidence: 0.6);
        var fatigue = FatigueResult.Empty;

        var result = VitalResult.Evaluate(heartRate, respiratory, fatigue, Now);

        Assert.True(result.IsCritical);
        Assert.Contains(result.CriticalReasons, reason => reason.Contains("cardiaca elevata"));
    }

    [Fact]
    public void Evaluate_NotCritical_WhenAllVitalsWithinNormalRange()
    {
        var heartRate = RppgEstimate.Create(bpm: 72, confidence: 0.9, windowSeconds: 20);
        var respiratory = RespiratoryEstimate.Create(breathsPerMinute: 15, confidence: 0.7);
        var fatigue = new FatigueResult(score: 5, perclos: 0.05, isCritical: false);

        var result = VitalResult.Evaluate(heartRate, respiratory, fatigue, Now);

        Assert.False(result.IsCritical);
        Assert.Empty(result.CriticalReasons);
        Assert.Equal(72, result.HeartRateBpm);
        Assert.Equal(15, result.RespiratoryRateBpm);
    }

    [Fact]
    public void Evaluate_ReportsNullHeartRate_WhenEstimateIsUnreliable()
    {
        var unreliableHeartRate = RppgEstimate.Insufficient(windowSeconds: 3);
        var respiratory = RespiratoryEstimate.Create(breathsPerMinute: 15, confidence: 0.7);
        var fatigue = FatigueResult.Empty;

        var result = VitalResult.Evaluate(unreliableHeartRate, respiratory, fatigue, Now);

        Assert.Null(result.HeartRateBpm);
        Assert.False(result.IsCritical); // an unreliable reading must not silently trigger a false alarm
    }

    [Fact]
    public void Evaluate_FlagsCritical_WhenFatigueIsHigh()
    {
        var heartRate = RppgEstimate.Create(bpm: 70, confidence: 0.9, windowSeconds: 20);
        var respiratory = RespiratoryEstimate.Create(breathsPerMinute: 15, confidence: 0.7);
        var fatigue = new FatigueResult(score: 30, perclos: 0.30, isCritical: true);

        var result = VitalResult.Evaluate(heartRate, respiratory, fatigue, Now);

        Assert.True(result.IsCritical);
        Assert.Contains(result.CriticalReasons, reason => reason.Contains("affaticamento"));
    }
}
