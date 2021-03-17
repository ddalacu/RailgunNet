using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace RailgunNet.Connection.Client
{
    /// <summary>
    ///     Packet sent from client to server.
    /// </summary>
    public sealed class RailPacketToServer : RailPacketOutgoing
    {
        private readonly RailPackedListOutgoing<RailCommandUpdate> commandUpdates;
        private readonly RailView view;

        public RailPacketToServer()
        {
            view = new RailView();
            commandUpdates = new RailPackedListOutgoing<RailCommandUpdate>();
        }

        public IEnumerable<RailCommandUpdate> Sent => commandUpdates.Sent;

        public override void Reset()
        {
            base.Reset();

            view.Clear();
            commandUpdates.Clear();
        }

        public void Populate(IEnumerable<RailCommandUpdate> commandUpdates, RailView view)
        {
            this.commandUpdates.AddPending(commandUpdates);

            // We don't care about sending/storing the local tick
            this.view.CopyFrom(view);
        }

        #region Encode/Decode
        public override void EncodePayload(
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer,
            Tick localTick,
            int reservedBytes)
        {
            // Write: [Commands]
            EncodeCommands(buffer);

            // Write: [View]
            EncodeView(buffer, localTick, reservedBytes);
        }

        private void EncodeCommands(RailBitBuffer buffer)
        {
            commandUpdates.Encode(
                buffer,
                RailConfig.PACKCAP_COMMANDS,
                RailConfig.MAXSIZE_COMMANDUPDATE,
                (commandUpdate, buf) => commandUpdate.Encode(buf));
        }

        private void EncodeView(RailBitBuffer buffer, Tick localTick, int reservedBytes)
        {
            buffer.PackToSize(
                RailConfig.PACKCAP_MESSAGE_TOTAL - reservedBytes,
                int.MaxValue,
                view.GetOrdered(localTick),
                (pair, buf) =>
                {
                    buf.WriteEntityId(pair.Key); // Write: [EntityId]
                    buf.WriteTick(pair.Value.LastReceivedTick); // Write: [LastReceivedTick]
                    // (Local tick not transmitted)
                    buf.WriteBool(pair.Value.IsFrozen); // Write: [IsFrozen]
                });
        }
        #endregion
    }
}
