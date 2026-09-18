using KineMotion.Contracts;
using KineMotion.Core.ValueObjects;

namespace KineMotion.Api.Mapping;

public static class PoseFrameMapper
{
    public static PoseFrame ToCore(this PoseFrameDto dto) => new(
        dto.TimestampMs, dto.ShoulderAbductionDeg, dto.ElbowFlexionDeg, dto.TrunkLeanDeg, dto.HandX, dto.HandY);
}
