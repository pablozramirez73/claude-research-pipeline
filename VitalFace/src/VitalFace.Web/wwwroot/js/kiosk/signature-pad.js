// Touch signature canvas for GDPR consent (see technical report, section 8). The PNG bytes never
// leave this module except as a one-way SHA-256 hash computed server-side on receipt
// (VitalFace.Core.Models.ConsentRecord) — the image itself is not retained after the request completes.

export function createSignaturePad(containerId) {
    let hasDrawn = false;
    let lastPoint = null;

    const sketch = (p) => {
        p.setup = () => {
            const container = document.getElementById(containerId);
            const canvas = p.createCanvas(container.clientWidth, container.clientHeight);
            canvas.parent(containerId);
            p.background(255);
            p.strokeWeight(3);
            p.stroke(10, 30, 80);
        };

        p.draw = () => {
            const active = p.mouseIsPressed || (p.touches && p.touches.length > 0);
            if (!active) {
                lastPoint = null;
                return;
            }

            const point = p.touches.length > 0
                ? { x: p.touches[0].x, y: p.touches[0].y }
                : { x: p.mouseX, y: p.mouseY };

            if (lastPoint) {
                p.line(lastPoint.x, lastPoint.y, point.x, point.y);
                hasDrawn = true;
            }
            lastPoint = point;
        };
    };

    const instance = new p5(sketch);

    return {
        hasSignature: () => hasDrawn,
        clear: () => { instance.background(255); hasDrawn = false; lastPoint = null; },
        getPngBytes: () => new Promise((resolve) => {
            instance.canvas.toBlob(async (blob) => {
                const buffer = await blob.arrayBuffer();
                resolve(new Uint8Array(buffer));
            }, 'image/png');
        }),
        destroy: () => instance.remove()
    };
}
