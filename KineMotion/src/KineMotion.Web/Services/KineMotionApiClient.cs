using System.Net.Http.Json;
using KineMotion.Contracts;

namespace KineMotion.Web.Services;

/// <summary>Thin typed wrapper over the Api's REST endpoints. No retry/circuit-breaker policy —
/// a kiosk-scale app talking to one backend doesn't need Polly yet (see README risk table).</summary>
public sealed class KineMotionApiClient(HttpClient http)
{
    public async Task<PrescriptionDto> CreateDemoPrescriptionAsync(Guid patientId, Guid therapistId, ExerciseTypeDto exercise, int targetReps, double targetRomDeg, CancellationToken ct = default)
    {
        var request = new CreatePrescriptionRequest(patientId, therapistId, exercise, targetReps, targetRomDeg);
        var response = await http.PostAsJsonAsync("api/prescriptions", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrescriptionDto>(ct))!;
    }

    public async Task<PrescriptionDto?> GetPrescriptionAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/prescriptions/{id}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PrescriptionDto>(ct);
    }

    public async Task<SessionSummaryDto> CompleteSessionAsync(CompleteSessionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/sessions/complete", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionSummaryDto>(ct))!;
    }
}
