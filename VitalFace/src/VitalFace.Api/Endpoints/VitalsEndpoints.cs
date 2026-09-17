using Microsoft.AspNetCore.Mvc;
using VitalFace.Contracts;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Models;

namespace VitalFace.Api.Endpoints;

public static class VitalsEndpoints
{
    public static RouteGroupBuilder MapVitalsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vitals").WithTags("Vitals");

        group.MapPost("/analyze", AnalyzeAsync)
            .WithName("AnalyzeVitals")
            .WithSummary("Runs CHROM/PERCLOS/respiration estimation over one capture window and persists the screening.")
            .Produces<VitalAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}/ticket", GetTicketAsync)
            .WithName("GetScreeningTicket")
            .WithSummary("Renders the printable pharmacy/RSA ticket for a completed screening.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> AnalyzeAsync(
        [FromBody] VitalAnalysisRequest request,
        IRppgProcessor rppgProcessor,
        IRespiratoryRateEstimator respiratoryEstimator,
        IFatigueDetector fatigueDetector,
        IScreeningRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.ConsentSignaturePng is not { Length: > 0 })
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ConsentSignaturePng)] = ["Il consenso (firma touch) e' obbligatorio prima dello screening."]
            });

        VitalSnapshot snapshot;
        try
        {
            snapshot = new VitalSnapshot
            {
                RedChannel = request.R,
                GreenChannel = request.G,
                BlueChannel = request.B,
                EyeOpenness = request.EyeOpenness,
                ShoulderY = request.ShoulderY,
                FrameRateHz = request.Fps,
                CapturedAtUtc = timeProvider.GetUtcNow()
            };
            snapshot.Validate();
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] });
        }

        var heartRate = rppgProcessor.EstimateHeartRate(snapshot.RedChannel, snapshot.GreenChannel, snapshot.BlueChannel, snapshot.FrameRateHz);
        var respiratoryRate = respiratoryEstimator.Estimate(snapshot.ShoulderY, snapshot.FrameRateHz);
        var fatigue = fatigueDetector.CalculatePerclos(snapshot.EyeOpenness, snapshot.EyeOpenness.Length / snapshot.FrameRateHz);

        var now = timeProvider.GetUtcNow();
        var result = VitalResult.Evaluate(heartRate, respiratoryRate, fatigue, now);
        var consent = ConsentRecord.FromSignatureBytes(request.ConsentSignaturePng, now);

        var screening = Screening.Create(request.LocationId, result, consent);
        await repository.AddAsync(screening, cancellationToken);

        return Results.Ok(new VitalAnalysisResponse(
            screening.Id,
            result.HeartRateBpm,
            result.RespiratoryRateBpm,
            result.FatigueScore,
            result.IsCritical,
            result.CriticalReasons,
            result.GeneratedAtUtc));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid id,
        IScreeningRepository repository,
        IScreeningTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var screening = await repository.GetByIdAsync(id, cancellationToken);
        if (screening is null)
            return Results.NotFound();

        var pdfBytes = await ticketService.GenerateTicketAsync(screening, cancellationToken);
        return Results.File(pdfBytes, "application/pdf", $"vitalface-{id}.pdf");
    }
}
