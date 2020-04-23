/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

using System.Collections.Generic;

namespace Railgun
{
    public class RailCommandUpdate
        : IRailPoolable<RailCommandUpdate>
    {
        private const int BUFFER_CAPACITY =
            RailConfig.COMMAND_SEND_COUNT;

        private static readonly int BUFFER_COUNT_BITS =
            RailUtil.Log2(BUFFER_CAPACITY) + 1;

        private readonly RailRollingBuffer<RailCommand> commands;

        public RailCommandUpdate()
        {
            EntityId = EntityId.INVALID;
            commands =
                new RailRollingBuffer<RailCommand>(
                    BUFFER_CAPACITY);
        }

#if CLIENT
        public IRailEntity Entity { get; set; }
#endif

        public EntityId EntityId { get; private set; }

        public IEnumerable<RailCommand> Commands => commands.GetValues();

        public static RailCommandUpdate Create(
            RailResource resource,
            EntityId entityId,
            IEnumerable<RailCommand> commands)
        {
            RailCommandUpdate update = resource.CreateCommandUpdate();
            update.Initialize(entityId, commands);
            return update;
        }

        private void Initialize(
            EntityId entityId,
            IEnumerable<RailCommand> outgoingCommands)
        {
            EntityId = entityId;
            foreach (RailCommand command in outgoingCommands)
                commands.Store(command);
        }

        private void Reset()
        {
            EntityId = EntityId.INVALID;
            commands.Clear();
        }

#if CLIENT
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
#endif

#if SERVER
        public static RailCommandUpdate Decode(
            RailResource resource,
            RailBitBuffer buffer)
        {
            RailCommandUpdate update = resource.CreateCommandUpdate();

            // Read: [EntityId]
            update.EntityId = buffer.ReadEntityId();

            // Read: [Count]
            int count = (int) buffer.Read(BUFFER_COUNT_BITS);

            // Read: [Commands]
            for (int i = 0; i < count; i++)
                update.commands.Store(RailCommand.Decode(resource, buffer));

            return update;
        }
#endif

        #region Pooling

        IRailMemoryPool<RailCommandUpdate> IRailPoolable<RailCommandUpdate>.Pool { get; set; }

        void IRailPoolable<RailCommandUpdate>.Reset()
        {
            Reset();
        }

        #endregion
    }
}