using Microsoft.EntityFrameworkCore;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;

namespace VitalFace.Infrastructure.Persistence;

public sealed class ScreeningRepository(VitalFaceDbContext dbContext) : IScreeningRepository
{
    public async Task AddAsync(Screening screening, CancellationToken cancellationToken = default)
    {
        dbContext.Screenings.Add(screening);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Screening?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Screenings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}
