using Microsoft.JSInterop;

namespace KineMotion.Web.Services;

/// <summary>Thin IJSRuntime wrapper over wwwroot/js/mediapipe/pose-game-bridge.js and wwwroot/js/games/p5-bird.js.</summary>
public sealed class PoseGameInterop(IJSRuntime js)
{
    public async Task<bool> StartPoseTrackingAsync(string videoElementId, DotNetObjectReference<object> callbackTarget)
    {
        return await js.InvokeAsync<bool>("KineMotionBridge.start", videoElementId, callbackTarget);
    }

    public async Task StopPoseTrackingAsync()
    {
        await js.InvokeVoidAsync("KineMotionBridge.stop");
    }

    public async Task StartShoulderBirdGameAsync(string containerElementId, DotNetObjectReference<object> callbackTarget)
    {
        await js.InvokeVoidAsync("KineMotionGames.startShoulderBird", containerElementId, callbackTarget);
    }

    public async Task StopGameAsync()
    {
        await js.InvokeVoidAsync("KineMotionGames.stop");
    }
}
