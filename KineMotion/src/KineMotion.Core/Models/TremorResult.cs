namespace KineMotion.Core.Models;

/// <param name="DominantFrequencyHz">Frequency with the most power inside the pathological tremor band.</param>
/// <param name="AmplitudeDeg">Peak spectral amplitude at <see cref="DominantFrequencyHz"/>, in the same units as the input signal (normalized landmark units, not degrees — see remarks on the caller).</param>
/// <param name="TremorIndex">Share of total signal power that falls inside <see cref="TremorBandLowHz"/>-<see cref="TremorBandHighHz"/> Hz. 0 = no tremor-band energy, 1 = all power is in-band.</param>
/// <param name="IsTremorPresent">True when both <see cref="TremorIndex"/> and <see cref="AmplitudeDeg"/> clear their thresholds — avoids flagging a flat, low-energy signal that happens to have a spectral peak in-band.</param>
public sealed record TremorResult(
    double DominantFrequencyHz,
    double AmplitudeDeg,
    double TremorIndex)
{
    /// <summary>Rest + postural/action tremor band (Parkinsonian rest tremor ~4-6 Hz, essential tremor ~6-12 Hz).</summary>
    public const double TremorBandLowHz = 3.0;
    public const double TremorBandHighHz = 12.0;
    public const double TremorIndexThreshold = 0.15;
    public const double MinAmplitudeThreshold = 0.003;

    public bool IsTremorPresent => TremorIndex >= TremorIndexThreshold && AmplitudeDeg >= MinAmplitudeThreshold;
}
