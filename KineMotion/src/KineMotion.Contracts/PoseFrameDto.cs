namespace KineMotion.Contracts;

/// <summary>Wire shape of one captured frame, sent from the Blazor client to the Api at session end.</summary>
public sealed record PoseFrameDto(
    double TimestampMs,
    double ShoulderAbductionDeg,
    double ElbowFlexionDeg,
    double TrunkLeanDeg,
    double HandX,
    double HandY);
