using KineMotion.Api.Exercises;
using KineMotion.Api.Mapping;
using KineMotion.Contracts;
using KineMotion.Core.Entities;
using KineMotion.Core.Models;
using KineMotion.Core.Services;
using KineMotion.Core.ValueObjects;
using KineMotion.Infrastructure.Persistence;

namespace KineMotion.Api.Endpoints;

public static class SessionsEndpoints
{
    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").WithTags("Sessions");

        group.MapPost("/complete", async (
            CompleteSessionRequest request,
            IPrescriptionRepository prescriptions,
            ISessionRepository sessions,
            RomCalculator romCalculator,
            TremorAnalyzer tremorAnalyzer,
            CancellationToken ct) =>
        {
            var prescription = await prescriptions.GetAsync(request.PrescriptionId, ct);
            if (prescription is null)
            {
                return Results.NotFound($"Prescription {request.PrescriptionId} not found.");
            }

            var session = new Session
            {
                PrescriptionId = request.PrescriptionId,
                PatientId = request.PatientId,
                TherapistId = request.TherapistId,
                Frames = request.Frames.Select(f => f.ToCore()).ToList(),
            };
            session.Complete(request.RepsCompleted);

            await sessions.AddAsync(session, ct);
            await sessions.SaveChangesAsync(ct);

            var summary = BuildSummary(session, prescription.Exercise, request.SampleRateHz, romCalculator, tremorAnalyzer);
            return Results.Ok(summary);
        });

        group.MapGet("/{sessionId:guid}/summary", async (
            Guid sessionId,
            double sampleRateHz,
            ISessionRepository sessions,
            IPrescriptionRepository prescriptions,
            RomCalculator romCalculator,
            TremorAnalyzer tremorAnalyzer,
            CancellationToken ct) =>
        {
            var session = await sessions.GetAsync(sessionId, ct);
            if (session is null)
            {
                return Results.NotFound($"Session {sessionId} not found.");
            }

            var prescription = await prescriptions.GetAsync(session.PrescriptionId, ct);
            if (prescription is null)
            {
                return Results.NotFound($"Prescription {session.PrescriptionId} not found.");
            }

            var summary = BuildSummary(session, prescription.Exercise, sampleRateHz, romCalculator, tremorAnalyzer);
            return Results.Ok(summary);
        });

        return app;
    }

    /// <summary>
    /// Applies whichever analyzers <see cref="ExerciseAnalysisPlan"/> says are relevant to this
    /// exercise. A session always has frames (validated client-side before it's ever posted), so
    /// an empty series here would be a client bug, not a normal empty-result case — it is left to
    /// throw via <see cref="RomCalculator"/>'s own guard rather than silently swallowed.
    /// </summary>
    private static SessionSummaryDto BuildSummary(
        Session session, ExerciseType exercise, double sampleRateHz, RomCalculator romCalculator, TremorAnalyzer tremorAnalyzer)
    {
        RomResult? rom = null;
        var romJoint = ExerciseAnalysisPlan.RomJointFor(exercise);
        if (romJoint is { } joint)
        {
            rom = romCalculator.Calculate(session.Frames, joint);
        }

        TremorResult? tremor = null;
        if (ExerciseAnalysisPlan.AnalyzeTremor(exercise))
        {
            tremor = tremorAnalyzer.Analyze(session.Frames, sampleRateHz);
        }

        return new SessionSummaryDto(
            session.Id,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.RepsCompleted,
            rom?.MaxAngleDeg ?? 0,
            rom?.MinAngleDeg ?? 0,
            rom?.SmoothnessScore ?? 0,
            rom?.CompensationDetected ?? false,
            rom?.CompensationRatio ?? 0,
            tremor?.DominantFrequencyHz,
            tremor?.TremorIndex,
            tremor?.IsTremorPresent);
    }
}
