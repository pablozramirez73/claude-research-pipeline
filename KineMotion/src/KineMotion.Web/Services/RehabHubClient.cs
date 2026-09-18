using KineMotion.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace KineMotion.Web.Services;

/// <summary>
/// Wraps the SignalR connection to <c>RehabHub</c>. Streaming a metric while disconnected (a
/// dropped wifi mid-game, for instance) is swallowed rather than surfaced: live biofeedback to
/// the therapist is a nice-to-have during play, and must never block or crash the patient's game
/// loop — the clinically authoritative data is the frame series posted at session end regardless.
/// </summary>
public sealed class RehabHubClient(IConfiguration configuration) : IAsyncDisposable
{
    private HubConnection? _connection;

    public async Task StartAsync(CancellationToken ct = default)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"] ?? throw new InvalidOperationException("Missing ApiBaseUrl configuration.");
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(apiBaseUrl), "/hubs/rehab"))
            .WithAutomaticReconnect()
            .Build();

        await _connection.StartAsync(ct);
    }

    public async Task StreamMetricAsync(PatientMetricDto metric, CancellationToken ct = default)
    {
        if (_connection is not { State: HubConnectionState.Connected })
        {
            return;
        }

        try
        {
            await _connection.SendAsync("StreamMetrics", metric, ct);
        }
        catch (Exception)
        {
            // Best-effort live telemetry; see class remarks.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
