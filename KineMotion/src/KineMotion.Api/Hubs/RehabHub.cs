using KineMotion.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace KineMotion.Api.Hubs;

/// <summary>
/// Real-time channel between a patient's game session and their assigned clinician's live view
/// (brief §7, "Clinician Portal ... Live View via SignalR"). Groups are keyed by therapist id: a
/// clinician joins their own group once and receives every one of their patients' metric updates;
/// a patient never joins a group, they only ever call <see cref="StreamMetrics"/>.
/// </summary>
public sealed class RehabHub(ILogger<RehabHub> logger) : Hub
{
    private static string TherapistGroup(Guid therapistId) => $"therapist:{therapistId}";

    /// <summary>Called by the clinician portal on connect to start receiving live updates for their own patients.</summary>
    public async Task JoinTherapistGroup(Guid therapistId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TherapistGroup(therapistId));
    }

    /// <summary>
    /// Called by the patient's browser, roughly once per animation frame, while a game is being
    /// played. Fire-and-forget by design: this is live biofeedback telemetry, not the clinical
    /// record — the authoritative session data (full frame series, ROM/tremor summary) is written
    /// once via POST /api/sessions/complete when the rep set ends, not accumulated here.
    /// </summary>
    public async Task StreamMetrics(PatientMetricDto metric)
    {
        logger.LogTrace(
            "Live metric patient={PatientId} therapist={TherapistId} angle={Angle:F1} reps={Reps}",
            metric.PatientId, metric.TherapistId, metric.CurrentAngleDeg, metric.RepsCompletedSoFar);

        await Clients.Group(TherapistGroup(metric.TherapistId)).SendAsync("MetricUpdate", metric);
    }
}
