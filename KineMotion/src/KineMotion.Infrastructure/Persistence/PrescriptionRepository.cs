using KineMotion.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace KineMotion.Infrastructure.Persistence;

public interface IPrescriptionRepository
{
    Task<Prescription?> GetAsync(Guid id, CancellationToken ct);
    Task<List<Prescription>> GetActiveForPatientAsync(Guid patientId, CancellationToken ct);
    Task AddAsync(Prescription prescription, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class PrescriptionRepository(KineMotionDbContext db) : IPrescriptionRepository
{
    public Task<Prescription?> GetAsync(Guid id, CancellationToken ct) =>
        db.Prescriptions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Prescription>> GetActiveForPatientAsync(Guid patientId, CancellationToken ct) =>
        db.Prescriptions.Where(p => p.PatientId == patientId && p.Active).ToListAsync(ct);

    public async Task AddAsync(Prescription prescription, CancellationToken ct) =>
        await db.Prescriptions.AddAsync(prescription, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
