﻿using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.System.Buffer;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic.Wrappers
{
    public class RailCommandUpdate : IRailPoolable<RailCommandUpdate>
    {
        private const int BUFFER_CAPACITY = RailConfig.COMMAND_SEND_COUNT;

        private static readonly int BUFFER_COUNT_BITS = RailUtil.Log2(BUFFER_CAPACITY) + 1;

        private readonly RailRollingBuffer<RailCommand> commands;

        public RailCommandUpdate()
        {
            EntityId = EntityId.INVALID;
            commands = new RailRollingBuffer<RailCommand>(BUFFER_CAPACITY);
        }
        public IProduceCommands Entity { get; private set; }

        public EntityId EntityId { get; private set; }

        public IEnumerable<RailCommand> Commands => commands.GetValues();

        public static RailCommandUpdate Create(
            IRailCommandConstruction commandCreator,
            IProduceCommands entity,
            IEnumerable<RailCommand> commands)
        {
            var update = commandCreator.CreateCommandUpdate();
            update.Initialize(entity.Id, commands);
            update.Entity = entity;
            return update;
        }

        private void Initialize(EntityId entityId, IEnumerable<RailCommand> outgoingCommands)
        {
            EntityId = entityId;
            foreach (RailCommand command in outgoingCommands)
            {
                commands.Store(command);
            }
        }

        private void Reset()
        {
            EntityId = EntityId.INVALID;
            commands.Clear();
        }
        public void Encode(RailBitBuffer buffer)
        {
            // Write: [EntityId]
            buffer.WriteEntityId(EntityId);

            // Write: [Count]
            buffer.Write(BUFFER_COUNT_BITS, (uint) commands.Count);

            // Write: [Commands]
            foreach (RailCommand command in commands.GetValues())
                command.Encode(buffer);
        }

        public static RailCommandUpdate Decode(
            IRailCommandConstruction commandCreator,
            RailBitBuffer buffer)
        {
            RailCommandUpdate update = commandCreator.CreateCommandUpdate();

            // Read: [EntityId]
            update.EntityId = buffer.ReadEntityId();

            // Read: [Count]
            int count = (int) buffer.Read(BUFFER_COUNT_BITS);

            // Read: [Commands]
            for (int i = 0; i < count; i++)
            {
                var command = commandCreator.CreateCommand();
                command.Decode(buffer);

                update.commands.Store(command);
            }

            return update;
        }

        #region Pooling
        IRailMemoryPool<RailCommandUpdate> IRailPoolable<RailCommandUpdate>.OwnerPool { get; set; }

        void IRailPoolable<RailCommandUpdate>.Reset()
        {
            Reset();
        }

        void IRailPoolable<RailCommandUpdate>.Allocated()
        {
        }
        #endregion
    }
}
