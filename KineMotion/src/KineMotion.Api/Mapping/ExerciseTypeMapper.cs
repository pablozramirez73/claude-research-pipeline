using CoreExercise = KineMotion.Core.ValueObjects.ExerciseType;
using KineMotion.Contracts;

namespace KineMotion.Api.Mapping;

/// <summary>Boundary mapping so Core's domain enum and Contracts' wire enum can evolve independently.</summary>
public static class ExerciseTypeMapper
{
    public static CoreExercise ToCore(this ExerciseTypeDto dto) => dto switch
    {
        ExerciseTypeDto.ShoulderBirdFlight => CoreExercise.ShoulderBirdFlight,
        ExerciseTypeDto.FruitPickReach => CoreExercise.FruitPickReach,
        ExerciseTypeDto.BalanceBoard => CoreExercise.BalanceBoard,
        ExerciseTypeDto.MirrorHand => CoreExercise.MirrorHand,
        ExerciseTypeDto.BreathFlower => CoreExercise.BreathFlower,
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto, null),
    };

    public static ExerciseTypeDto ToDto(this CoreExercise core) => core switch
    {
        CoreExercise.ShoulderBirdFlight => ExerciseTypeDto.ShoulderBirdFlight,
        CoreExercise.FruitPickReach => ExerciseTypeDto.FruitPickReach,
        CoreExercise.BalanceBoard => ExerciseTypeDto.BalanceBoard,
        CoreExercise.MirrorHand => ExerciseTypeDto.MirrorHand,
        CoreExercise.BreathFlower => ExerciseTypeDto.BreathFlower,
        _ => throw new ArgumentOutOfRangeException(nameof(core), core, null),
    };
}
