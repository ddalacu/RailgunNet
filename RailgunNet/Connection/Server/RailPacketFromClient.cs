using System.Collections.Generic;
using System.Linq;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;
using UnityEngine;

namespace RailgunNet.Connection.Server
{

    /// <summary>
    ///     Packet from the client received by the server. Corresponding packet on client
    ///     side is RailPacketToServer.
    /// </summary>
    public class RailPacketFromClient : RailPacketIncoming
    {
        private readonly RailPackedListIncoming<RailCommandUpdate> commandUpdates;

        public RailPackedListIncoming<RailCommandUpdate> CommandUpdated => commandUpdates;

        public RailPacketFromClient()
        {
            View = new RailView();
            commandUpdates = new RailPackedListIncoming<RailCommandUpdate>();
        }

        public RailView View { get; }


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

                    var railViewEntry = new RailViewEntry(tick, isFrozen);

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
