// ShoulderBirdFlight: bird height is driven by window.KineMotionBridge.currentAngle, updated by
// pose-game-bridge.js at MediaPipe's detection rate. Reading it directly here (rather than
// round-tripping every render frame through Blazor/.NET) keeps the game loop at p5's own 60fps
// regardless of detection latency — see KineMotion/README.md for why capture (.NET side) and
// rendering (this file) are deliberately decoupled.
//
// Same sandbox caveat as pose-game-bridge.js: p5.js itself is loaded from cdn.jsdelivr.net in
// wwwroot/index.html, which this environment's network policy blocks — the sketch below was
// written against p5.js 1.x's documented instance-mode API but its rendering has not been
// visually verified against a real running p5 instance in this session.
(function () {
    const GRAVITY_LERP = 0.12;
    const PIPE_SPEED_PX_PER_FRAME = 3;
    const PIPE_GAP_PX = 220;
    const PIPE_SPACING_PX = 320;
    const PIPE_WIDTH_PX = 60;
    const BIRD_RADIUS_PX = 20;

    let p5Instance = null;
    let onRepCallbackRef = null;
    let score = 0;

    function sketch(p) {
        let birdY = 0;
        let pipes = []; // { x, gapCenterY, scored }
        let canvasWidth = 800;
        let canvasHeight = 500;
        let gameOver = false;

        p.setup = function () {
            const container = document.getElementById(p._kineMotionContainerId);
            canvasWidth = container ? container.clientWidth || 800 : 800;
            canvasHeight = 500;
            p.createCanvas(canvasWidth, canvasHeight);
            birdY = canvasHeight / 2;
            spawnPipe(canvasWidth + 100);
        };

        function spawnPipe(x) {
            const margin = PIPE_GAP_PX / 2 + 30;
            const gapCenterY = p.random(margin, canvasHeight - margin);
            pipes.push({ x, gapCenterY, scored: false });
        }

        p.draw = function () {
            p.background(135, 206, 235);

            if (gameOver) {
                p.fill(0);
                p.textAlign(p.CENTER, p.CENTER);
                p.textSize(28);
                p.text("Sessione conclusa", canvasWidth / 2, canvasHeight / 2);
                return;
            }

            // 0deg (arm at side) -> bottom of screen, 180deg (arm overhead) -> top.
            const targetAngle = (window.KineMotionBridge && window.KineMotionBridge.currentAngle) || 0;
            const targetY = p.map(p.constrain(targetAngle, 0, 180), 0, 180, canvasHeight - BIRD_RADIUS_PX, BIRD_RADIUS_PX);
            birdY = p.lerp(birdY, targetY, GRAVITY_LERP);

            for (let i = pipes.length - 1; i >= 0; i--) {
                const pipe = pipes[i];
                pipe.x -= PIPE_SPEED_PX_PER_FRAME;

                p.fill(76, 175, 80);
                p.noStroke();
                const gapTop = pipe.gapCenterY - PIPE_GAP_PX / 2;
                const gapBottom = pipe.gapCenterY + PIPE_GAP_PX / 2;
                p.rect(pipe.x, 0, PIPE_WIDTH_PX, gapTop);
                p.rect(pipe.x, gapBottom, PIPE_WIDTH_PX, canvasHeight - gapBottom);

                const birdX = 100;
                if (!pipe.scored && pipe.x + PIPE_WIDTH_PX < birdX) {
                    pipe.scored = true;
                    score++;
                    if (onRepCallbackRef) {
                        onRepCallbackRef.invokeMethodAsync("OnRepCompleted", score).catch(() => {
                            window.KineMotionGames.stop();
                        });
                    }
                }

                const hitsPipeX = birdX + BIRD_RADIUS_PX > pipe.x && birdX - BIRD_RADIUS_PX < pipe.x + PIPE_WIDTH_PX;
                const hitsPipeY = birdY - BIRD_RADIUS_PX < gapTop || birdY + BIRD_RADIUS_PX > gapBottom;
                if (hitsPipeX && hitsPipeY) {
                    // A miss just costs a visual "bump"; there is no fail-state in a rehab game —
                    // the point is completing prescribed reps, not punishing imprecise ROM.
                    p.fill(255, 0, 0, 80);
                    p.circle(birdX, birdY, BIRD_RADIUS_PX * 2 + 10);
                }

                if (pipe.x + PIPE_WIDTH_PX < 0) pipes.splice(i, 1);
            }

            if (pipes.length === 0 || pipes[pipes.length - 1].x < canvasWidth - PIPE_SPACING_PX) {
                spawnPipe(canvasWidth);
            }

            p.fill(255, 235, 59);
            p.circle(100, birdY, BIRD_RADIUS_PX * 2);

            p.fill(0);
            p.textSize(20);
            p.textAlign(p.LEFT, p.TOP);
            p.text(`Ripetizioni: ${score}`, 10, 10);
        };

        p.endGame = function () {
            gameOver = true;
        };
    }

    window.KineMotionGames = {
        startShoulderBird(containerElementId, callbackRef) {
            this.stop();
            score = 0;
            onRepCallbackRef = callbackRef;

            const wrapped = (p) => {
                p._kineMotionContainerId = containerElementId;
                sketch(p);
            };
            p5Instance = new p5(wrapped, containerElementId);
        },

        stop() {
            if (p5Instance) {
                if (p5Instance.endGame) p5Instance.endGame();
                p5Instance.remove();
                p5Instance = null;
            }
            onRepCallbackRef = null;
        },
    };
})();
