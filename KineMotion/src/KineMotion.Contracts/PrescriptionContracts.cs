namespace KineMotion.Contracts;

public sealed record PrescriptionDto(
    Guid Id,
    Guid PatientId,
    Guid TherapistId,
    ExerciseTypeDto Exercise,
    int TargetReps,
    double TargetRomDeg,
    bool Active,
    DateTime CreatedAtUtc);

public sealed record CreatePrescriptionRequest(
    Guid PatientId,
    Guid TherapistId,
    ExerciseTypeDto Exercise,
    int TargetReps,
    double TargetRomDeg);
