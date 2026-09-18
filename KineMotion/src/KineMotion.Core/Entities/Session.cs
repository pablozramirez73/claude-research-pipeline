using KineMotion.Core.ValueObjects;

namespace KineMotion.Core.Entities;

/// <summary>
/// One completed play-through of a prescribed exercise. <see cref="Frames"/> is the full frame
/// series captured client-side; it is persisted as a JSON column (see
/// KineMotion.Infrastructure/Persistence/SessionConfiguration.cs) rather than one row per frame,
/// since it is always read/written whole and never queried per-frame.
/// </summary>
public sealed class Session
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid PrescriptionId { get; set; }
    public required Guid PatientId { get; set; }
    public required Guid TherapistId { get; set; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int RepsCompleted { get; set; }
    public List<PoseFrame> Frames { get; set; } = [];

    public void Complete(int repsCompleted)
    {
        if (EndedAtUtc is not null)
        {
            throw new InvalidOperationException($"Session {Id} was already completed at {EndedAtUtc:o}.");
        }

        RepsCompleted = repsCompleted;
        EndedAtUtc = DateTime.UtcNow;
    }
}
