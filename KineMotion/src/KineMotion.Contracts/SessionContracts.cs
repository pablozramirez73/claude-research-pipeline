namespace KineMotion.Contracts;

/// <summary>Sent once, when the patient finishes a rep set — the full captured frame series plus how many reps they completed.</summary>
public sealed record CompleteSessionRequest(
    Guid PrescriptionId,
    Guid PatientId,
    Guid TherapistId,
    int RepsCompleted,
    double SampleRateHz,
    List<PoseFrameDto> Frames);

/// <summary>Server-computed clinical summary of a session, ready for the clinician dashboard or the patient's own progress view.</summary>
public sealed record SessionSummaryDto(
    Guid SessionId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int RepsCompleted,
    double MaxAngleDeg,
    double MinAngleDeg,
    double SmoothnessScore,
    bool CompensationDetected,
    double CompensationRatio,
    double? TremorDominantFrequencyHz,
    double? TremorIndex,
    bool? TremorPresent);
