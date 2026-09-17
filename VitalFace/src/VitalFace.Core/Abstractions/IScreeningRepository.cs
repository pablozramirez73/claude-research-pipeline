using VitalFace.Core.Models;

namespace VitalFace.Core.Abstractions;

/// <summary>Persistence port for <see cref="Screening"/>, implemented in VitalFace.Infrastructure (EF Core / PostgreSQL).</summary>
public interface IScreeningRepository
{
    Task AddAsync(Screening screening, CancellationToken cancellationToken = default);
    Task<Screening?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
