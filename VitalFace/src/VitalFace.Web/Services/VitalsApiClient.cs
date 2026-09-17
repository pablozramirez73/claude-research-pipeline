using System.Net.Http.Json;
using VitalFace.Contracts;

namespace VitalFace.Web.Services;

public sealed class VitalsApiClient(HttpClient httpClient)
{
    public async Task<VitalAnalysisResponse> AnalyzeAsync(VitalAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/vitals/analyze", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VitalAnalysisResponse>(cancellationToken))!;
    }

    public Uri GetTicketUri(Guid screeningId) => new(httpClient.BaseAddress!, $"api/vitals/{screeningId}/ticket");
}
