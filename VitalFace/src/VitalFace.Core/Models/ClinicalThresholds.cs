namespace VitalFace.Core.Models;

/// <summary>
/// Conservative "flag for human follow-up" bands for a Class I non-invasive pre-triage device.
/// These are NOT diagnostic thresholds — they only decide whether a screening should be
/// escalated to a pharmacist / occupational physician. Intentionally configurable per deployment
/// (a logistics warehouse and an RSA may want different bands), so they are exposed as
/// <c>static readonly</c> defaults rather than hard-coded inline in the evaluation logic.
/// </summary>
public static class ClinicalThresholds
{
    /// <summary>Resting adult bradycardia red-flag (bpm).</summary>
    public static double BradycardiaBpm { get; set; } = 50;

    /// <summary>Resting adult tachycardia red-flag (bpm).</summary>
    public static double TachycardiaBpm { get; set; } = 120;

    /// <summary>Bradypnea red-flag (breaths/min).</summary>
    public static double BradypneaBpm { get; set; } = 10;

    /// <summary>Tachypnea red-flag (breaths/min), NEWS2-inspired banding.</summary>
    public static double TachypneaBpm { get; set; } = 24;

    /// <summary>PERCLOS drowsiness red-flag (Wierwille &amp; Ellsworth / FHWA reference).</summary>
    public static double PerclosCriticalThreshold { get; set; } = 0.15;
}
