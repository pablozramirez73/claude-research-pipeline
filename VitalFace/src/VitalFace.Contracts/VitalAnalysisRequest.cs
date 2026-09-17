namespace VitalFace.Contracts;

/// <summary>
/// Payload sent by the kiosk at the end of a ~30s capture window. Contains only per-frame scalar
/// aggregates (mean ROI colour, landmark-derived openness/position) — never pixel or video data,
/// per the privacy-by-design requirement (see technical report, section 8).
/// </summary>
public sealed record VitalAnalysisRequest(
    string LocationId,
    float[] R,
    float[] G,
    float[] B,
    float[] EyeOpenness,
    float[] ShoulderY,
    double Fps,
    byte[] ConsentSignaturePng);
