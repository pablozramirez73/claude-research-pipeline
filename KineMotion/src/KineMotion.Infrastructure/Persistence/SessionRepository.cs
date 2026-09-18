using KineMotion.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace KineMotion.Infrastructure.Persistence;

public interface ISessionRepository
{
    Task<Session?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Session session, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class SessionRepository(KineMotionDbContext db) : ISessionRepository
{
    public Task<Session?> GetAsync(Guid id, CancellationToken ct) =>
        db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Session session, CancellationToken ct) =>
        await db.Sessions.AddAsync(session, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
