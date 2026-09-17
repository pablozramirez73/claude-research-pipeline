// Owns the single webcam capture (shared with face-bridge.js for MediaPipe inference) and renders
// it through a WEBGL fragment shader that amplifies small colour deviations from a running mean —
// a simplified, real-time analogue of Eulerian Video Magnification, purely for the operator-facing
// "you can see it working" visualization. It never feeds the actual CHROM measurement, which runs
// on the raw per-frame ROI means captured independently in face-bridge.js.

export function createRppgAmplifier(containerId) {
    let amplifyShader;
    let capture;
    let meanColor = [0.5, 0.5, 0.5];
    let resolveReady;
    const ready = new Promise(resolve => { resolveReady = resolve; });

    const sketch = (p) => {
        p.preload = () => {
            amplifyShader = p.loadShader('js/kiosk/shaders/vert.glsl', 'js/kiosk/shaders/frag_rppg.glsl');
        };

        p.setup = () => {
            const container = document.getElementById(containerId);
            const canvas = p.createCanvas(container.clientWidth, container.clientHeight, p.WEBGL);
            canvas.parent(containerId);
            p.noStroke();

            capture = p.createCapture({ video: { facingMode: 'user' }, audio: false }, () => resolveReady(capture.elt));
            capture.size(640, 480);
            capture.hide(); // we render it ourselves, amplified, via the shader below
        };

        p.draw = () => {
            if (!capture || capture.elt.readyState < 2) {
                p.background(8, 12, 24);
                return;
            }

            p.shader(amplifyShader);
            amplifyShader.setUniform('tex', capture);
            amplifyShader.setUniform('meanColor', meanColor);
            amplifyShader.setUniform('amplification', 6.0);
            p.rect(-p.width / 2, -p.height / 2, p.width, p.height);

            // Cheap sparse running-mean update so the amplification stays centered without a full
            // GPU reduction pass — this is a visualization aid, not the measurement pipeline.
            capture.loadPixels();
            const pixels = capture.pixels;
            if (pixels && pixels.length > 0) {
                let sumR = 0, sumG = 0, sumB = 0, count = 0;
                for (let i = 0; i < pixels.length; i += 4 * 97) {
                    sumR += pixels[i]; sumG += pixels[i + 1]; sumB += pixels[i + 2];
                    count++;
                }
                if (count > 0) {
                    const alpha = 0.05;
                    meanColor = [
                        meanColor[0] * (1 - alpha) + (sumR / count / 255) * alpha,
                        meanColor[1] * (1 - alpha) + (sumG / count / 255) * alpha,
                        meanColor[2] * (1 - alpha) + (sumB / count / 255) * alpha
                    ];
                }
            }
        };
    };

    const instance = new p5(sketch);

    return {
        ready: () => ready,
        getVideoElement: () => capture?.elt,
        destroy: () => instance.remove()
    };
}
