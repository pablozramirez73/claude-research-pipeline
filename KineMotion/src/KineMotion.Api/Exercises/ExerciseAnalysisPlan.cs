using KineMotion.Core.ValueObjects;

namespace KineMotion.Api.Exercises;

/// <summary>
/// Which Core analyzer(s) apply to each game. Two of the five MVP games — BalanceBoard and
/// BreathFlower — don't have a dedicated signal in <see cref="PoseFrame"/> yet (center-of-mass
/// sway and chest expansion respectively): BalanceBoard is approximated here with trunk lean as
/// a stand-in, and BreathFlower gets no clinical summary at all in this scaffold. Both are
/// documented deviations, not oversights — see KineMotion/README.md "Cosa non è stato implementato".
/// </summary>
public static class ExerciseAnalysisPlan
{
    public static JointAngle? RomJointFor(ExerciseType exercise) => exercise switch
    {
        ExerciseType.ShoulderBirdFlight => JointAngle.ShoulderAbduction,
        ExerciseType.FruitPickReach => JointAngle.ElbowFlexion,
        ExerciseType.BalanceBoard => JointAngle.TrunkLean,
        ExerciseType.MirrorHand => null,
        ExerciseType.BreathFlower => null,
        _ => throw new ArgumentOutOfRangeException(nameof(exercise), exercise, null),
    };

    public static bool AnalyzeTremor(ExerciseType exercise) => exercise == ExerciseType.MirrorHand;
}
