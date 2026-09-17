using VitalFace.Core.Services.SignalProcessing;

namespace VitalFace.Core.Tests.Services;

public class DigitalSignalProcessingTests
{
    [Fact]
    public void RemoveLinearTrend_ZeroesOutAPureRamp()
    {
        double[] signal = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

        DigitalSignalProcessing.RemoveLinearTrend(signal);

        foreach (var sample in signal)
            Assert.Equal(0, sample, precision: 9);
    }

    [Fact]
    public void FindDominantFrequency_LocatesPeakOfAPureSineWithinBand()
    {
        const double fps = 30.0;
        const double signalHz = 1.25; // 75 bpm
        int n = 600; // 20s
        var signal = new double[n];
        for (int i = 0; i < n; i++)
            signal[i] = Math.Sin(2 * Math.PI * signalHz * i / fps);

        // Windowing (as the real estimators do) suppresses the leakage a bare rectangular/zero-padded
        // FFT would otherwise spread across the band from truncating a non-integer number of cycles.
        DigitalSignalProcessing.ApplyHannWindow(signal);

        var spectrum = DigitalSignalProcessing.ForwardFft(signal);
        var (frequencyHz, confidence) = DigitalSignalProcessing.FindDominantFrequency(spectrum, fps, minHz: 0.7, maxHz: 4.0);

        Assert.InRange(frequencyHz, signalHz - 0.1, signalHz + 0.1);
        Assert.True(confidence > 0.9, $"Expected a near-pure sine to dominate the band, got confidence={confidence:F2}.");
    }

    [Fact]
    public void FindDominantFrequency_ReturnsZero_WhenBandIsEmpty()
    {
        var spectrum = DigitalSignalProcessing.ForwardFft(new double[64]);

        var (frequencyHz, confidence) = DigitalSignalProcessing.FindDominantFrequency(spectrum, frameRateHz: 30.0, minHz: 10.0, maxHz: 5.0);

        Assert.Equal(0, frequencyHz);
        Assert.Equal(0, confidence);
    }
}
