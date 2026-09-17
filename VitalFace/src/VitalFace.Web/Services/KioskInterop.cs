using Microsoft.JSInterop;
using VitalFace.Web.Models;

namespace VitalFace.Web.Services;

/// <summary>
/// Typed wrapper around the wwwroot/js/kiosk/kiosk-interop.js ES module. Kept as the single seam
/// between Razor components and JS, so the kiosk page never calls IJSRuntime directly.
/// </summary>
public sealed class KioskInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/kiosk/kiosk-interop.js").AsTask());

    public async Task InitializeCaptureStageAsync(string videoContainerId, string vizContainerId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("initializeCaptureStage", videoContainerId, vizContainerId);
    }

    public async Task StartCaptureAsync()
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("startCapture");
    }

    public async Task<CaptureBuffers?> PeekBuffersAsync()
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<CaptureBuffers?>("peekBuffers");
    }

    public async Task<CaptureBuffers> StopCaptureAsync()
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<CaptureBuffers>("stopCapture");
    }

    public async Task UpdateLiveMetricsAsync(double bpm, double respPhase, double perclos, double confidence)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("updateLiveMetrics", bpm, respPhase, perclos, confidence);
    }

    public async Task InitializeConsentAsync(string containerId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("initializeConsent", containerId);
    }

    public async Task ClearSignatureAsync()
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("clearSignature");
    }

    public async Task<bool> HasConsentSignatureAsync()
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<bool>("hasConsentSignature");
    }

    public async Task<byte[]> GetConsentSignatureBytesAsync()
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<byte[]>("getConsentSignatureBytes");
    }

    public async Task RequestFullscreenAsync()
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("requestKioskFullscreen");
    }

    /// <summary>Stops the camera/MediaPipe/p5 instances between customers. The JS module stays loaded.</summary>
    public async Task TeardownAsync()
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("teardown");
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("teardown");
            await module.DisposeAsync();
        }
    }
}
