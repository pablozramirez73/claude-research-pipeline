// Simplified, real-time colour-magnification fragment shader: amplifies each pixel's deviation
// from a slowly-updated running mean colour (computed on the JS side and passed in as a uniform).
// This is a visualization aid only — a coarse, fast analogue of Eulerian Video Magnification —
// not the actual CHROM pulse measurement, which is computed from raw per-frame ROI means.
precision highp float;

varying vec2 vTexCoord;

uniform sampler2D tex;
uniform vec3 meanColor;
uniform float amplification;

void main() {
    vec4 color = texture2D(tex, vTexCoord);
    vec3 deviation = color.rgb - meanColor;
    vec3 amplified = meanColor + deviation * amplification;
    gl_FragColor = vec4(clamp(amplified, 0.0, 1.0), color.a);
}
