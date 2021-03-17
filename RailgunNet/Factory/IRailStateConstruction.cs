using System;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding.Compressors;

namespace RailgunNet.Factory
{
    public interface IRailStateConstruction
    {
        RailIntCompressor EntityTypeCompressor { get; }

        EntityType GetEntityType(RailState state);

        RailState CreateState(EntityType factoryType);

        RailStateDelta CreateDelta();
        RailStateRecord CreateRecord();
    }
}
