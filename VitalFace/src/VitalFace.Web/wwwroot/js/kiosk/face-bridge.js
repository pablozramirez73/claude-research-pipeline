// Wraps MediaPipe FaceLandmarker (478 points + blendshapes) and PoseLandmarker (lite, shoulders
// only) around a single shared <video> element. Extracts only scalar aggregates per frame — mean
// ROI colour, eye openness, shoulder Y — never pixel buffers, and never any data that could be
// used to reconstruct the subject's face (see technical report, section 8: Sicurezza e Compliance).

import { FaceLandmarker, PoseLandmarker, FilesetResolver } from 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.22/vision_bundle.mjs';

// Approximate forehead / cheek polygons on the 478-point Face Landmarker topology. These are a
// reasonable rPPG-literature starting point (bounding-box sample, not a precise face mask) — tune
// against the official topology diagram and the deployment's camera distance/lighting before
// clinical-adjacent use.
const FOREHEAD_INDICES = [10, 108, 151, 337];
const LEFT_CHEEK_INDICES = [50, 101, 36, 205];
const RIGHT_CHEEK_INDICES = [280, 330, 266, 425];
const LEFT_SHOULDER_INDEX = 11; // Pose Landmarker (BlazePose topology): left shoulder
const RIGHT_SHOULDER_INDEX = 12; // right shoulder

const WASM_BASE_URL = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.22/wasm';
const FACE_MODEL_URL = 'models/face_landmarker.task';
const POSE_MODEL_URL = 'models/pose_landmarker_lite.task';

export async function createFaceBridge(videoElement) {
    const filesetResolver = await FilesetResolver.forVisionTasks(WASM_BASE_URL);

    const faceLandmarker = await FaceLandmarker.createFromOptions(filesetResolver, {
        baseOptions: { modelAssetPath: FACE_MODEL_URL, delegate: 'GPU' },
        outputFaceBlendshapes: true,
        outputFacialTransformationMatrixes: false,
        runningMode: 'VIDEO',
        numFaces: 1
    });

    const poseLandmarker = await PoseLandmarker.createFromOptions(filesetResolver, {
        baseOptions: { modelAssetPath: POSE_MODEL_URL, delegate: 'GPU' },
        runningMode: 'VIDEO',
        numPoses: 1
    });

    const offscreen = document.createElement('canvas');
    const offscreenCtx = offscreen.getContext('2d', { willReadFrequently: true });

    let capturing = false;
    let rafHandle = null;
    let frameCount = 0;
    let captureStartMs = 0;
    let disposed = false;
    const buffers = { r: [], g: [], b: [], eyeOpenness: [], shoulderY: [] };

    function meanRoiColor(pixels, width, indices, landmarks) {
        // Bounding-box sample of the polygon — fast and sufficient at kiosk distance/lighting,
        // versus a full point-in-polygon mask which would cost far more per frame for little gain.
        let minX = 1, minY = 1, maxX = 0, maxY = 0;
        for (const idx of indices) {
            const lm = landmarks[idx];
            minX = Math.min(minX, lm.x); maxX = Math.max(maxX, lm.x);
            minY = Math.min(minY, lm.y); maxY = Math.max(maxY, lm.y);
        }

        const x0 = Math.max(0, Math.floor(minX * width));
        const x1 = Math.min(width, Math.ceil(maxX * width));
        const y0 = Math.max(0, Math.floor(minY * offscreen.height));
        const y1 = Math.min(offscreen.height, Math.ceil(maxY * offscreen.height));

        let sumR = 0, sumG = 0, sumB = 0, count = 0;
        for (let y = y0; y < y1; y += 2) { // stride-2 sampling keeps this cheap at 30fps
            for (let x = x0; x < x1; x += 2) {
                const i = (y * width + x) * 4;
                sumR += pixels[i]; sumG += pixels[i + 1]; sumB += pixels[i + 2];
                count++;
            }
        }
        return count > 0 ? [sumR / count, sumG / count, sumB / count] : [0, 0, 0];
    }

    function processFrame(timestampMs) {
        if (disposed) return;

        if (videoElement.readyState < 2) {
            rafHandle = requestAnimationFrame(processFrame);
            return;
        }

        if (capturing) {
            if (offscreen.width !== videoElement.videoWidth) {
                offscreen.width = videoElement.videoWidth;
                offscreen.height = videoElement.videoHeight;
            }

            const faceResult = faceLandmarker.detectForVideo(videoElement, timestampMs);
            const poseResult = poseLandmarker.detectForVideo(videoElement, timestampMs);

            if (faceResult.faceLandmarks?.length > 0) {
                const landmarks = faceResult.faceLandmarks[0];
                offscreenCtx.drawImage(videoElement, 0, 0, offscreen.width, offscreen.height);
                const frame = offscreenCtx.getImageData(0, 0, offscreen.width, offscreen.height).data;

                const [fR, fG, fB] = meanRoiColor(frame, offscreen.width, FOREHEAD_INDICES, landmarks);
                const [lR, lG, lB] = meanRoiColor(frame, offscreen.width, LEFT_CHEEK_INDICES, landmarks);
                const [rR, rG, rB] = meanRoiColor(frame, offscreen.width, RIGHT_CHEEK_INDICES, landmarks);

                buffers.r.push((fR + lR + rR) / 3);
                buffers.g.push((fG + lG + rG) / 3);
                buffers.b.push((fB + lB + rB) / 3);

                const blendshapes = faceResult.faceBlendshapes?.[0]?.categories ?? [];
                const scoreOf = (name) => blendshapes.find(c => c.categoryName === name)?.score ?? 0;
                const eyeOpenness = 1 - (scoreOf('eyeBlinkLeft') + scoreOf('eyeBlinkRight')) / 2;
                buffers.eyeOpenness.push(eyeOpenness);

                if (poseResult.landmarks?.length > 0) {
                    const pose = poseResult.landmarks[0];
                    buffers.shoulderY.push((pose[LEFT_SHOULDER_INDEX].y + pose[RIGHT_SHOULDER_INDEX].y) / 2);
                } else {
                    buffers.shoulderY.push(buffers.shoulderY.at(-1) ?? 0.5);
                }

                frameCount++;
            }
        }

        rafHandle = requestAnimationFrame(processFrame);
    }

    rafHandle = requestAnimationFrame(processFrame);

    return {
        startCapture() {
            buffers.r = []; buffers.g = []; buffers.b = [];
            buffers.eyeOpenness = []; buffers.shoulderY = [];
            frameCount = 0;
            captureStartMs = performance.now();
            capturing = true;
        },

        peekBuffers() {
            if (!capturing || buffers.r.length === 0) return null;
            const elapsedSeconds = (performance.now() - captureStartMs) / 1000;
            const fps = elapsedSeconds > 0 ? frameCount / elapsedSeconds : 30;
            return {
                r: [...buffers.r], g: [...buffers.g], b: [...buffers.b],
                eyeOpenness: [...buffers.eyeOpenness], shoulderY: [...buffers.shoulderY],
                fps
            };
        },

        stopCapture() {
            capturing = false;
            const elapsedSeconds = (performance.now() - captureStartMs) / 1000;
            const fps = elapsedSeconds > 0 ? frameCount / elapsedSeconds : 30;
            return {
                r: buffers.r, g: buffers.g, b: buffers.b,
                eyeOpenness: buffers.eyeOpenness, shoulderY: buffers.shoulderY,
                fps
            };
        },

        dispose() {
            disposed = true;
            if (rafHandle) cancelAnimationFrame(rafHandle);
            faceLandmarker.close();
            poseLandmarker.close();
        }
    };
}
