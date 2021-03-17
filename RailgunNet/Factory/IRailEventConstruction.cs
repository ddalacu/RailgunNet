﻿using RailgunNet.Logic;
using RailgunNet.System.Encoding.Compressors;

namespace RailgunNet.Factory
{
    public interface IRailEventConstruction
    {
        RailIntCompressor EventTypeCompressor { get; }
        RailEvent CreateEvent(EventType iFactoryType);

        T CreateEvent<T>()
            where T : RailEvent;
    }
}
