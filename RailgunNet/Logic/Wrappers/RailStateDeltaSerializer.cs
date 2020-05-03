using RailgunNet.Factory;
using RailgunNet.Logic.State;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util;

namespace RailgunNet.Logic.Wrappers
{
    public static class RailStateDeltaSerializer
    {
        [OnlyIn(Component.Server)]
        public static void EncodeDelta(
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer,
            RailStateDelta delta)
        {
            // Write: [EntityId]
            buffer.WriteEntityId(delta.EntityId);

            // Write: [IsFrozen]
            buffer.WriteBool(delta.IsFrozen);

            if (delta.IsFrozen == false)
            {
                // Write: [FactoryType]
                RailState state = delta.State;
                buffer.WriteInt(stateCreator.EntityTypeCompressor, state.FactoryType);

                // Write: [IsRemoved]
                buffer.WriteBool(delta.RemovedTick.IsValid);

                if (delta.RemovedTick.IsValid)
                    // Write: [RemovedTick]
                {
                    buffer.WriteTick(delta.RemovedTick);
                }

                // Write: [HasControllerData]
                buffer.WriteBool(state.HasControllerData);

                // Write: [HasImmutableData]
                buffer.WriteBool(state.HasImmutableData);

                // Write: [Flags]
                buffer.Write(state.FlagBits, state.Flags);

                // Write: [Mutable Data]
                state.EncodeMutableData(buffer, state.Flags);

                if (state.HasControllerData)
                {
                    // Write: [Controller Data]
                    state.EncodeControllerData(buffer);

                    // Write: [Command Ack]
                    buffer.WriteTick(delta.CommandAck);
                }

                if (state.HasImmutableData)
                    // Write: [Immutable Data]
                {
                    state.EncodeImmutableData(buffer);
                }
            }
        }

        [OnlyIn(Component.Client)]
        public static RailStateDelta DecodeDelta(
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer,
            Tick packetTick)
        {
            RailStateDelta delta = stateCreator.CreateDelta();
            RailState state = null;

            Tick commandAck = Tick.INVALID;
            Tick removedTick = Tick.INVALID;

            // Read: [EntityId]
            EntityId entityId = buffer.ReadEntityId();

            // Read: [IsFrozen]
            bool isFrozen = buffer.ReadBool();

            if (isFrozen == false)
            {
                // Read: [FactoryType]
                int factoryType = buffer.ReadInt(stateCreator.EntityTypeCompressor);
                state = RailState.Create(stateCreator, factoryType);

                // Read: [IsRemoved]
                bool isRemoved = buffer.ReadBool();

                if (isRemoved)
                    // Read: [RemovedTick]
                {
                    removedTick = buffer.ReadTick();
                }

                // Read: [HasControllerData]
                state.HasControllerData = buffer.ReadBool();

                // Read: [HasImmutableData]
                state.HasImmutableData = buffer.ReadBool();

                // Read: [Flags]
                state.Flags = buffer.Read(state.FlagBits);

                // Read: [Mutable Data]
                state.DecodeMutableData(buffer, state.Flags);

                if (state.HasControllerData)
                {
                    // Read: [Controller Data]
                    state.DecodeControllerData(buffer);

                    // Read: [Command Ack]
                    commandAck = buffer.ReadTick();
                }

                if (state.HasImmutableData)
                    // Read: [Immutable Data]
                {
                    state.DecodeImmutableData(buffer);
                }
            }

            delta.Initialize(packetTick, entityId, state, removedTick, commandAck, isFrozen);
            return delta;
        }
    }
}
