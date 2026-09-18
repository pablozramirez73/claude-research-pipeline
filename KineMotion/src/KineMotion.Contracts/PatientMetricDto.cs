namespace KineMotion.Contracts;

/// <summary>
/// Live, per-frame telemetry pushed from the patient's browser to <c>RehabHub.StreamMetrics</c> over
/// SignalR while a game is being played, and re-broadcast to the assigned therapist's group for the
/// "live view" — this is the wire type the brief's <c>PatientMetric</c> parameter refers to.
/// Intentionally minimal: no landmarks, no video, just the angle the current game is scoring against.
/// </summary>
public sealed record PatientMetricDto(
    Guid PatientId,
    Guid TherapistId,
    double TimestampMs,
    double CurrentAngleDeg,
    int RepsCompletedSoFar);
