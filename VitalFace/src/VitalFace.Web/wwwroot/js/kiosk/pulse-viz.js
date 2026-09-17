// Medical-grade-looking live readout: a circle that pulses in sync with the current BPM estimate,
// a respiratory sine wave, and a PERCLOS fatigue bar. Driven entirely by values pushed from C#
// (updateLiveMetrics), which itself runs the same CHROM/PERCLOS estimators (compiled to WASM) on
// the rolling capture buffer — this is a visualization of real numbers, not a canned animation.

export function createPulseViz(containerId) {
    let latest = { bpm: 0, respPhase: 0, perclos: 0, confidence: 0 };

    const sketch = (p) => {
        let phase = 0;

        p.setup = () => {
            const container = document.getElementById(containerId);
            const canvas = p.createCanvas(container.clientWidth, container.clientHeight, p.WEBGL);
            canvas.parent(containerId);
        };

        p.draw = () => {
            p.background(8, 12, 24);

            const bpmHz = latest.bpm > 0 ? latest.bpm / 60 : 1;
            phase += (p.deltaTime / 1000) * bpmHz * p.TWO_PI;
            const pulseScale = 1 + 0.12 * Math.max(0, Math.sin(phase));

            p.push();
            p.noStroke();
            const alpha = 120 + 100 * latest.confidence;
            p.fill(220, 60, 70, alpha);
            p.circle(0, 0, p.height * 0.35 * pulseScale);
            p.pop();

            p.push();
            p.translate(-p.width / 2, p.height / 2 - 40);
            p.stroke(90, 180, 255);
            p.strokeWeight(3);
            p.noFill();
            p.beginShape();
            for (let x = 0; x <= p.width; x += 4) {
                const y = 20 * Math.sin((x / p.width) * p.TWO_PI * 2 + latest.respPhase);
                p.vertex(x, y);
            }
            p.endShape();
            p.pop();

            p.push();
            p.translate(-p.width / 2 + 20, -p.height / 2 + 20);
            p.noStroke();
            p.fill(60);
            p.rect(0, 0, 200, 16);
            const critical = latest.perclos > 15;
            p.fill(critical ? p.color(220, 60, 60) : p.color(90, 200, 120));
            p.rect(0, 0, 200 * Math.min(1, latest.perclos / 100), 16);
            p.pop();
        };
    };

    const instance = new p5(sketch);

    return {
        update: (bpm, respPhase, perclos, confidence) => { latest = { bpm, respPhase, perclos, confidence }; },
        destroy: () => instance.remove()
    };
}
