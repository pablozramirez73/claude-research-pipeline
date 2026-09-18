// Bridges MediaPipe Tasks Vision (PoseLandmarker, VIDEO running mode) to Blazor. Reduces raw
// landmarks to the handful of scalars KineMotion.Contracts.PoseFrameDto needs — no landmark
// array, no pixel data, ever leaves this module (see KineMotion/README.md, "Sicurezza").
//
// NOT independently verified end-to-end in this sandbox: the agent environment's network policy
// blocks cdn.jsdelivr.net, so the MediaPipe Tasks Vision bundle and its .task model can't actually
// be fetched here. The landmark indices and angle math below follow the published Tasks Vision
// PoseLandmarker API (33-point BlazePose topology) and were reviewed for correctness, but the
// live camera -> detection -> angle pipeline needs an acceptance test on real hardware with real
// network access before shipping.
(function () {
    const POSE_LANDMARKER_TASK_URL = "/models/pose_landmarker_lite.task";
    const VISION_WASM_BASE_URL =
        "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm";
    const VISION_BUNDLE_URL =
        "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/vision_bundle.mjs";

    // BlazePose 33-point topology (Tasks Vision PoseLandmarker output order).
    const LANDMARK = {
        LEFT_SHOULDER: 11, RIGHT_SHOULDER: 12,
        LEFT_ELBOW: 13, RIGHT_ELBOW: 14,
        LEFT_WRIST: 15, RIGHT_WRIST: 16,
        LEFT_HIP: 23, RIGHT_HIP: 24,
    };

    let poseLandmarker = null;
    let videoEl = null;
    let stream = null;
    let rafHandle = null;
    let dotNetRef = null;
    let sessionStartMs = null;

    function angleBetweenDeg(ax, ay, bx, by) {
        const dot = ax * bx + ay * by;
        const magA = Math.hypot(ax, ay);
        const magB = Math.hypot(bx, by);
        if (magA === 0 || magB === 0) return 0;
        const cos = Math.min(1, Math.max(-1, dot / (magA * magB)));
        return (Math.acos(cos) * 180) / Math.PI;
    }

    // Shoulder abduction: angle between the torso's vertical axis (hip -> shoulder) and the
    // upper arm (shoulder -> elbow), on the patient's dominant/tracked side (right, mirrored view).
    function shoulderAbductionDeg(lm) {
        const shoulder = lm[LANDMARK.RIGHT_SHOULDER];
        const elbow = lm[LANDMARK.RIGHT_ELBOW];
        const hip = lm[LANDMARK.RIGHT_HIP];
        const torsoX = shoulder.x - hip.x, torsoY = shoulder.y - hip.y;
        const armX = elbow.x - shoulder.x, armY = elbow.y - shoulder.y;
        return angleBetweenDeg(torsoX, torsoY, armX, armY);
    }

    function elbowFlexionDeg(lm) {
        const shoulder = lm[LANDMARK.RIGHT_SHOULDER];
        const elbow = lm[LANDMARK.RIGHT_ELBOW];
        const wrist = lm[LANDMARK.RIGHT_WRIST];
        const upperX = shoulder.x - elbow.x, upperY = shoulder.y - elbow.y;
        const forearmX = wrist.x - elbow.x, forearmY = wrist.y - elbow.y;
        return angleBetweenDeg(upperX, upperY, forearmX, forearmY);
    }

    // Lateral trunk lean from vertical: angle between the hip-midpoint -> shoulder-midpoint
    // segment and the image's vertical axis. Used both as BalanceBoard's proxy signal and as the
    // ShoulderBirdFlight compensation flag (leaning instead of abducting the arm).
    function trunkLeanDeg(lm) {
        const shoulderMidX = (lm[LANDMARK.LEFT_SHOULDER].x + lm[LANDMARK.RIGHT_SHOULDER].x) / 2;
        const shoulderMidY = (lm[LANDMARK.LEFT_SHOULDER].y + lm[LANDMARK.RIGHT_SHOULDER].y) / 2;
        const hipMidX = (lm[LANDMARK.LEFT_HIP].x + lm[LANDMARK.RIGHT_HIP].x) / 2;
        const hipMidY = (lm[LANDMARK.LEFT_HIP].y + lm[LANDMARK.RIGHT_HIP].y) / 2;
        const dx = shoulderMidX - hipMidX, dy = shoulderMidY - hipMidY;
        // Vertical reference is (0, -1) in image space (y grows downward).
        return angleBetweenDeg(dx, dy, 0, -1);
    }

    async function loadVisionModule() {
        const visionModule = await import(VISION_BUNDLE_URL);
        const { FilesetResolver, PoseLandmarker } = visionModule;
        const filesetResolver = await FilesetResolver.forVisionTasks(VISION_WASM_BASE_URL);
        return PoseLandmarker.createFromOptions(filesetResolver, {
            baseOptions: { modelAssetPath: POSE_LANDMARKER_TASK_URL },
            runningMode: "VIDEO",
            numPoses: 1,
            minPoseDetectionConfidence: 0.7,
            minPosePresenceConfidence: 0.7,
            minTrackingConfidence: 0.7,
        });
    }

    function detectionLoop() {
        if (!poseLandmarker || !videoEl) return;

        const nowMs = performance.now();
        const result = poseLandmarker.detectForVideo(videoEl, nowMs);

        if (result.landmarks && result.landmarks.length > 0) {
            const lm = result.landmarks[0];
            const wrist = lm[LANDMARK.RIGHT_WRIST];

            const frame = {
                timestampMs: nowMs - sessionStartMs,
                shoulderAbductionDeg: shoulderAbductionDeg(lm),
                elbowFlexionDeg: elbowFlexionDeg(lm),
                trunkLeanDeg: trunkLeanDeg(lm),
                handX: wrist.x,
                handY: wrist.y,
            };

            window.KineMotionBridge.currentAngle = frame.shoulderAbductionDeg;
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnFrame", frame).catch(() => {
                    // Component was disposed mid-frame (navigated away) — stop quietly.
                    window.KineMotionBridge.stop();
                });
            }
        }

        rafHandle = requestAnimationFrame(detectionLoop);
    }

    window.KineMotionBridge = {
        currentAngle: 0,

        async start(videoElementId, callbackRef) {
            videoEl = document.getElementById(videoElementId);
            if (!videoEl) return false;

            try {
                stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "user" }, audio: false });
                videoEl.srcObject = stream;
                await videoEl.play();

                poseLandmarker = await loadVisionModule();
                dotNetRef = callbackRef;
                sessionStartMs = performance.now();
                rafHandle = requestAnimationFrame(detectionLoop);
                return true;
            } catch (err) {
                console.error("KineMotionBridge.start failed", err);
                this.stop();
                return false;
            }
        },

        stop() {
            if (rafHandle) cancelAnimationFrame(rafHandle);
            rafHandle = null;
            if (stream) stream.getTracks().forEach((track) => track.stop());
            stream = null;
            if (poseLandmarker) poseLandmarker.close();
            poseLandmarker = null;
            dotNetRef = null;
        },
    };
})();
