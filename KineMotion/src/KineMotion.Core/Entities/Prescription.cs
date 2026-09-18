using KineMotion.Core.ValueObjects;

namespace KineMotion.Core.Entities;

/// <summary>A clinician-authored exercise assignment: which game, how many reps, what ROM target.</summary>
public sealed class Prescription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid PatientId { get; set; }
    public required Guid TherapistId { get; set; }
    public required ExerciseType Exercise { get; set; }
    public required int TargetReps { get; set; }
    public required double TargetRomDeg { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
