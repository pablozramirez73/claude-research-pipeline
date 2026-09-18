using KineMotion.Api.Mapping;
using KineMotion.Contracts;
using KineMotion.Core.Entities;
using KineMotion.Infrastructure.Persistence;

namespace KineMotion.Api.Endpoints;

public static class PrescriptionsEndpoints
{
    public static IEndpointRouteBuilder MapPrescriptionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prescriptions").WithTags("Prescriptions");

        group.MapPost("/", async (CreatePrescriptionRequest request, IPrescriptionRepository repository, CancellationToken ct) =>
        {
            var prescription = new Prescription
            {
                PatientId = request.PatientId,
                TherapistId = request.TherapistId,
                Exercise = request.Exercise.ToCore(),
                TargetReps = request.TargetReps,
                TargetRomDeg = request.TargetRomDeg,
            };

            await repository.AddAsync(prescription, ct);
            await repository.SaveChangesAsync(ct);

            return TypedResults.Created($"/api/prescriptions/{prescription.Id}", ToDto(prescription));
        });

        group.MapGet("/by-patient/{patientId:guid}/active", async (Guid patientId, IPrescriptionRepository repository, CancellationToken ct) =>
        {
            var prescriptions = await repository.GetActiveForPatientAsync(patientId, ct);
            return TypedResults.Ok(prescriptions.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid id, IPrescriptionRepository repository, CancellationToken ct) =>
        {
            var prescription = await repository.GetAsync(id, ct);
            return prescription is null ? Results.NotFound() : Results.Ok(ToDto(prescription));
        });

        return app;
    }

    private static PrescriptionDto ToDto(Prescription p) => new(
        p.Id, p.PatientId, p.TherapistId, p.Exercise.ToDto(), p.TargetReps, p.TargetRomDeg, p.Active, p.CreatedAtUtc);
}
