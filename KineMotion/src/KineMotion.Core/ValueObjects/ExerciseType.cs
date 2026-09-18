namespace KineMotion.Core.ValueObjects;

/// <summary>The five MVP gamified exercises, each mapped to a distinct MediaPipe-derived signal.</summary>
public enum ExerciseType
{
    ShoulderBirdFlight,
    FruitPickReach,
    BalanceBoard,
    MirrorHand,
    BreathFlower,
}

/// <summary>Which joint-angle (or derived) series a calculator should read off a <see cref="PoseFrame"/>.</summary>
public enum JointAngle
{
    ShoulderAbduction,
    ElbowFlexion,
    TrunkLean,
}
