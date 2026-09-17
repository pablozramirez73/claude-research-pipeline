using VitalFace.Core.Models;

namespace VitalFace.Core.Abstractions;

/// <summary>Renders a printable pharmacy/RSA ticket for a completed screening (implemented with QuestPDF in Infrastructure).</summary>
public interface IScreeningTicketService
{
    Task<byte[]> GenerateTicketAsync(Screening screening, CancellationToken cancellationToken = default);
}
