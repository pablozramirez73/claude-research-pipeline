using System.Text.Json.Serialization;

namespace KineMotion.Contracts;

/// <summary>Wire-level mirror of <c>KineMotion.Core.ValueObjects.ExerciseType</c>. Contracts stays
/// dependency-free (no reference to Core) so the Blazor WASM client only ships DTOs, never Core's
/// algorithm code — KineMotion.Api maps between the two (see Api/Mapping/ExerciseTypeMapper.cs).
/// Serialized as a string (not the default ordinal int) so the wire payload stays readable and
/// safe to reorder members in — see the 500 this caught in KineMotion/README.md verification notes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExerciseTypeDto>))]
public enum ExerciseTypeDto
{
    ShoulderBirdFlight,
    FruitPickReach,
    BalanceBoard,
    MirrorHand,
    BreathFlower,
}
