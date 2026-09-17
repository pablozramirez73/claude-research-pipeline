namespace VitalFace.Web.Models;

/// <summary>Mirrors the object shape returned by the kiosk-interop.js `stopCapture`/`peekBuffers` calls.</summary>
public sealed record CaptureBuffers(
    float[] R,
    float[] G,
    float[] B,
    float[] EyeOpenness,
    float[] ShoulderY,
    double Fps);
