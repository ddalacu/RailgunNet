using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;

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
    public class RailPacketFromClient : RailPacketIncoming, IRailClientPacket
    {
        private readonly RailPackedListIncoming<RailCommandUpdate> commandUpdates;

        public RailPacketFromClient()
        {
            View = new RailView();
            commandUpdates = new RailPackedListIncoming<RailCommandUpdate>();
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
            IRailCommandConstruction commandCreator,
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer)
        {
            // Read: [Commands]
            DecodeCommands(commandCreator, buffer);

            // Read: [View]
            DecodeView(buffer);
        }

        private void DecodeCommands(IRailCommandConstruction commandCreator, RailBitBuffer buffer)
        {
            commandUpdates.Decode(buffer, buf => RailCommandUpdate.Decode(commandCreator, buf));
        }

        private void DecodeView(RailBitBuffer buffer)
        {
            var decoded = buffer.UnpackAll(
                buf =>
                {
                    var entityId = buf.ReadEntityId(); // Read: [EntityId] 
                    var tick = buf.ReadTick(); // Read: [Tick] 
                    var isFrozen = buf.ReadBool();// Read: [IsFrozen]

                    var railViewEntry = new RailViewEntry(tick, Tick.INVALID, isFrozen);

                    return new KeyValuePair<EntityId, RailViewEntry>(entityId, railViewEntry);
                }
            );

            foreach (KeyValuePair<EntityId, RailViewEntry> pair in decoded)
            {
                View.RecordUpdate(pair.Key, pair.Value);
            }
        }
    }
}
