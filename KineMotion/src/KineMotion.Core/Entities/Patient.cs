namespace KineMotion.Core.Entities;

public sealed class Patient
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TherapistId { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>ICD-10 code driving default exercise/ROM targets (e.g. "I69.351" post-stroke hemiparesis).</summary>
    public string? DiagnosisIcd10 { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
