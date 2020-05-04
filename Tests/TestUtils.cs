using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Moq;
using RailgunNet.Connection;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.State;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;

namespace Tests
{
    public class TestUtils
    {
        public static Mock<IRailCommandConstruction> CommandConstructionMock()
        {
            Mock<IRailCommandConstruction> mock = new Mock<IRailCommandConstruction>();
            mock.Setup(f => f.CreateCommand()).Returns(new Command());
            mock.Setup(f => f.CreateCommandUpdate()).Returns(new RailCommandUpdate());
            return mock;
        }

        public static Mock<IRailEventConstruction> EventConstructionMock()
        {
            Mock<IRailEventConstruction> mock = new Mock<IRailEventConstruction>();
            mock.Setup(f => f.CreateEvent(0)).Returns(new Event());
            mock.Setup(f => f.EventTypeCompressor).Returns(new RailIntCompressor(0, 2));
            return mock;
        }

        public static Mock<IRailStateConstruction> StateConstructionMock()
        {
            Mock<IRailStateConstruction> mock = new Mock<IRailStateConstruction>();
            mock.Setup(f => f.CreateState(0)).Returns(new RailStateGeneric<State>());
            mock.Setup(f => f.CreateDelta()).Returns(new RailStateDelta());
            mock.Setup(f => f.CreateRecord()).Returns(new RailStateRecord());
            mock.Setup(f => f.EntityTypeCompressor).Returns(new RailIntCompressor(0, 2));
            return mock;
        }

        public static Tick CreateTick(uint uiValue)
        {
            // Ticks can, by design, not be created from a raw value. We can work around this since
            // we know that ticks are serialized as uint.
            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            bitBuffer.WriteUInt(uiValue);
            return Tick.Read(bitBuffer);
        }
        public static SequenceId CreateSequenceId(int iValue)
        {
            return SequenceId.Start + iValue;
        }

        public class Command : RailCommand<Command>
        {
            private readonly int initialData;
            public int Data;

            public Command() : this(0)
            {
            }

            public Command(int i)
            {
                Data = i;
                initialData = i;
            }

            protected override void DecodeData(RailBitBuffer buffer)
            {
                Data = buffer.ReadInt();
            }

            protected override void EncodeData(RailBitBuffer buffer)
            {
                buffer.WriteInt(Data);
            }

            protected override void ResetData()
            {
                Data = initialData;
            }

            protected override void CopyDataFrom(Command other)
            {
                Data = other.Data;
            }
        }

        public class Event : RailEvent
        {
            protected override void Execute(RailRoom room, RailController sender)
            {
            }
        }

        public class State
        {
        }

        public class RailPacketComparer : IEqualityComparer<RailPacketBase>
        {
            public bool Equals([AllowNull] RailPacketBase x, [AllowNull] RailPacketBase y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                bool bSameSenderTick = x.SenderTick == y.SenderTick;
                bool bSameAckTick = x.LastAckTick == y.LastAckTick;
                bool bSameAckEventId = x.LastAckEventId == y.LastAckEventId;

                return bSameSenderTick && bSameAckTick && bSameAckEventId;
            }

            public int GetHashCode([DisallowNull] RailPacketBase packet)
            {
                return packet.SenderTick.GetHashCode() ^
                       packet.LastAckTick.GetHashCode() ^
                       packet.LastAckEventId.GetHashCode();
            }
        }
    }
}
