using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;

namespace VitalFace.Infrastructure.Pdf;

/// <summary>Renders the pharmacy/RSA thermal-printer-sized ticket for a screening (QuestPDF).</summary>
public sealed class ScreeningTicketService : IScreeningTicketService
{
    public Task<byte[]> GenerateTicketAsync(Screening screening, CancellationToken cancellationToken = default)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(12);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().AlignCenter().Text("VitalFace Station").FontSize(14).Bold();
                    header.Item().AlignCenter().Text("Referto di pre-triage contactless").FontSize(9);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(4);
                    content.Item().Text($"Sede: {screening.LocationId}");
                    content.Item().Text($"Data: {screening.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}");
                    content.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                    content.Item().Text(row =>
                    {
                        row.Span("Frequenza cardiaca: ").SemiBold();
                        row.Span(screening.HeartRateBpm is { } hr ? $"{hr:F0} bpm" : "n/d — segnale non affidabile");
                    });
                    content.Item().Text(row =>
                    {
                        row.Span("Frequenza respiratoria: ").SemiBold();
                        row.Span(screening.RespiratoryRateBpm is { } rr ? $"{rr:F0} atti/min" : "n/d — segnale non affidabile");
                    });
                    content.Item().Text(row =>
                    {
                        row.Span("Indice di affaticamento (PERCLOS): ").SemiBold();
                        row.Span($"{screening.FatigueScore:F0}/100");
                    });

                    if (screening.IsCritical)
                    {
                        content.Item().PaddingTop(8).Background(Colors.Red.Lighten4).Padding(6).Column(alert =>
                        {
                            alert.Item().Text("ATTENZIONE — valori fuori range").Bold().FontColor(Colors.Red.Darken2);
                            foreach (var reason in screening.CriticalReasons)
                                alert.Item().Text($"- {reason}").FontSize(9);
                        });
                    }

                    content.Item().PaddingTop(10).Text(
                            "Questo dispositivo esegue un pre-triage non invasivo (dispositivo medico Classe I) " +
                            "e NON sostituisce una diagnosi medica. In caso di valori anomali o sintomi, consultare un medico.")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text($"ID referto: {screening.Id}").FontSize(6).FontColor(Colors.Grey.Medium);
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
