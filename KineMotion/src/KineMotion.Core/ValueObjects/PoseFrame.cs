namespace KineMotion.Core.ValueObjects;

/// <summary>
/// One sampled instant of a rehab session, already reduced from raw MediaPipe landmarks to the
/// handful of clinically meaningful scalars on the browser side (pose-game-bridge.js). Nothing
/// upstream of this type ever carries pixels, video, or raw landmark coordinates — see
/// KineMotion.Web/wwwroot/js/mediapipe/pose-game-bridge.js for the landmark-to-angle math.
/// </summary>
/// <param name="TimestampMs">Milliseconds since the session's first frame.</param>
/// <param name="ShoulderAbductionDeg">Shoulder abduction angle (0 = arm at side, 180 = overhead).</param>
/// <param name="ElbowFlexionDeg">Elbow flexion angle (0 = fully extended).</param>
/// <param name="TrunkLeanDeg">Lateral trunk lean from vertical; used to flag compensation.</param>
/// <param name="HandX">Wrist landmark X, normalized to frame width, mean-centered per session.</param>
/// <param name="HandY">Wrist landmark Y, normalized to frame height, mean-centered per session.</param>
public sealed record PoseFrame(
    double TimestampMs,
    double ShoulderAbductionDeg,
    double ElbowFlexionDeg,
    double TrunkLeanDeg,
    double HandX,
    double HandY)
{
    public double AngleDeg(JointAngle joint) => joint switch
    {
        JointAngle.ShoulderAbduction => ShoulderAbductionDeg,
        JointAngle.ElbowFlexion => ElbowFlexionDeg,
        JointAngle.TrunkLean => TrunkLeanDeg,
        _ => throw new ArgumentOutOfRangeException(nameof(joint), joint, null),
    };
}
