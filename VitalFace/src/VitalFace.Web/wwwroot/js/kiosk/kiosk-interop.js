// Single entry point Blazor imports as an ES module (see Services/KioskInterop.cs). Wires together
// the camera/shader visualization (p5-rppg-shader.js), the MediaPipe pipeline (face-bridge.js), the
// live metrics readout (pulse-viz.js) and the GDPR consent pad (signature-pad.js) behind a small,
// stable surface so the Razor component never touches the individual modules directly.

import { createFaceBridge } from './face-bridge.js';
import { createRppgAmplifier } from './p5-rppg-shader.js';
import { createPulseViz } from './pulse-viz.js';
import { createSignaturePad } from './signature-pad.js';

let amplifier = null;
let faceBridge = null;
let pulseViz = null;
let signaturePad = null;

export async function initializeCaptureStage(videoContainerId, vizContainerId) {
    amplifier = createRppgAmplifier(videoContainerId);
    const videoElement = await amplifier.ready();
    faceBridge = await createFaceBridge(videoElement);
    pulseViz = createPulseViz(vizContainerId);
}

export function startCapture() {
    faceBridge?.startCapture();
}

export function peekBuffers() {
    return faceBridge?.peekBuffers() ?? null;
}

export function stopCapture() {
    return faceBridge?.stopCapture() ?? { r: [], g: [], b: [], eyeOpenness: [], shoulderY: [], fps: 30 };
}

export function updateLiveMetrics(bpm, respPhase, perclos, confidence) {
    pulseViz?.update(bpm, respPhase, perclos, confidence);
}

export function initializeConsent(containerId) {
    signaturePad?.destroy();
    signaturePad = createSignaturePad(containerId);
}

export function clearSignature() {
    signaturePad?.clear();
}

export function hasConsentSignature() {
    return signaturePad?.hasSignature() ?? false;
}

export async function getConsentSignatureBytes() {
    return signaturePad ? await signaturePad.getPngBytes() : new Uint8Array();
}

export function requestKioskFullscreen() {
    const el = document.documentElement;
    if (el.requestFullscreen) {
        el.requestFullscreen().catch(() => { /* kiosk hardware may already be locked down at the OS level */ });
    }
}

// Tears down the camera, MediaPipe tasks and p5 sketches between customers (or on idle-timeout
// reset). The ES module itself stays loaded — initializeCaptureStage() rebuilds everything fresh
// for the next screening.
export function teardown() {
    faceBridge?.dispose();
    amplifier?.destroy();
    pulseViz?.destroy();
    signaturePad?.destroy();
    faceBridge = null;
    amplifier = null;
    pulseViz = null;
    signaturePad = null;
}
