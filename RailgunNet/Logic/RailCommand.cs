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

using JetBrains.Annotations;
using RailgunNet.Factory;
using RailgunNet.System.Buffer;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    /// <summary>
    ///     This is the class to override to attach user-defined data to an entity.
    /// </summary>
    public abstract class RailCommand<T> : RailCommand
        where T : RailCommand<T>, new()
    {
        #region Casting Overrides

        protected override void SetDataFrom(RailCommand other)
        {
            CopyDataFrom((T) other);
        }

        #endregion

        protected abstract void CopyDataFrom(T other);
    }

    /// <summary>
    ///     Commands contain input data from the client to be applied to entities.
    /// </summary>
    public abstract class RailCommand :
        IRailPoolable<RailCommand>,
        IRailTimedValue
    {
        /// <summary>
        ///     The client's local tick (not server predicted) at the time of sending.
        /// </summary>
        public Tick ClientTick { get; set; } // Synchronized

        public bool IsNewCommand { get; set; }

        #region Implementation: IRailTimedValue

        Tick IRailTimedValue.Tick => ClientTick;

        #endregion

        #region To be implemented by API consumer.

        [PublicAPI]
        protected abstract void SetDataFrom(RailCommand other);

        [PublicAPI]
        protected abstract void WriteData(RailBitBuffer buffer);

        [PublicAPI]
        protected abstract void ReadData(RailBitBuffer buffer);

        [PublicAPI]
        protected abstract void ResetData();

        #endregion

        #region Implementation: IRailPoolable

        IRailMemoryPool<RailCommand> IRailPoolable<RailCommand>.Pool { get; set; }

        void IRailPoolable<RailCommand>.Reset()
        {
            Reset();
        }

        #endregion

        #region Encode/Decode/internals

        private void Reset()
        {
            ClientTick = Tick.INVALID;
            ResetData();
        }

        [OnlyIn(Component.Client)]
        public void Encode(
            RailBitBuffer buffer)
        {
            // Write: [SenderTick]
            buffer.WriteTick(ClientTick);

            // Write: [Command Data]
            WriteData(buffer);
        }

        [OnlyIn(Component.Server)]
        public static RailCommand Decode(
            ICommandCreator creator,
            RailBitBuffer buffer)
        {
            RailCommand command = creator.CreateCommand();

            // Read: [SenderTick]
            command.ClientTick = buffer.ReadTick();

            // Read: [Command Data]
            command.ReadData(buffer);

            return command;
        }

        #endregion
    }
}