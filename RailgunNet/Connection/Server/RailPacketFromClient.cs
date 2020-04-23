using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace RailgunNet.Connection.Server
{
    public interface IRailClientPacket
    {
        IEnumerable<RailCommandUpdate> CommandUpdates { get; }
    }

    /// <summary>
    ///     Packet from the client received by the server. Corresponding packet on client
    ///     side is RailPacketToServer.
    /// </summary>
    public class RailPacketFromClient
        : RailPacketIncoming
        , IRailClientPacket
    {
        private readonly RailPackedListC2S<RailCommandUpdate> commandUpdates;

        public RailPacketFromClient()
        {
            View = new RailView();
            commandUpdates = new RailPackedListC2S<RailCommandUpdate>();
        }

        public RailView View { get; }

        #region Interface

        IEnumerable<RailCommandUpdate> IRailClientPacket.CommandUpdates => commandUpdates.Received;

        #endregion

        public override void Reset()
        {
            base.Reset();

            View.Clear();
            commandUpdates.Clear();
        }
        public override void DecodePayload(
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Read: [Commands]
            DecodeCommands(resource, buffer);

            // Read: [View]
            DecodeView(buffer);
        }
        private void DecodeCommands(
            RailResource resource,
            RailBitBuffer buffer)
        {
            commandUpdates.Decode(
                buffer,
                () => RailCommandUpdate.Decode(resource, buffer));
        }
        private void DecodeView(RailBitBuffer buffer)
        {
            IEnumerable<KeyValuePair<EntityId, RailViewEntry>> decoded =
                buffer.UnpackAll(
                    () =>
                        new KeyValuePair<EntityId, RailViewEntry>(
                            buffer.ReadEntityId(), // Read: [EntityId] 
                            new RailViewEntry(
                                buffer.ReadTick(), // Read: [LastReceivedTick]
                                Tick.INVALID, // (Local tick not transmitted)
                                buffer.ReadBool())) // Read: [IsFrozen]
                );

            foreach (KeyValuePair<EntityId, RailViewEntry> pair in decoded)
                View.RecordUpdate(pair.Key, pair.Value);
        }
    }
}