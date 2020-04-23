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

namespace Railgun
{
    /// <summary>
    ///     Commands contain input data from the client to be applied to entities.
    /// </summary>
    public abstract class RailCommand :
        IRailPoolable<RailCommand>, IRailTimedValue
    {
        #region Pooling

        IRailMemoryPool<RailCommand> IRailPoolable<RailCommand>.Pool { get; set; }

        void IRailPoolable<RailCommand>.Reset()
        {
            Reset();
        }

        #endregion

        public static RailCommand Create(RailResource resource)
        {
            return resource.CreateCommand();
        }

        #region Interface

        Tick IRailTimedValue.Tick => ClientTick;

        #endregion

        protected abstract void SetDataFrom(RailCommand other);

        /// <summary>
        ///     The client's local tick (not server predicted) at the time of sending.
        /// </summary>
        public Tick ClientTick { get; set; } // Synchronized

        protected abstract void EncodeData(RailBitBuffer buffer);
        protected abstract void DecodeData(RailBitBuffer buffer);
        protected abstract void ResetData();

        public bool IsNewCommand { get; set; }

        private void Reset()
        {
            ClientTick = Tick.INVALID;
            ResetData();
        }

        #region Encode/Decode/etc.

#if CLIENT
        public void Encode(
          RailBitBuffer buffer)
        {
            // Write: [SenderTick]
            buffer.WriteTick(this.ClientTick);

            // Write: [Command Data]
            this.EncodeData(buffer);
        }
#endif

#if SERVER
        public static RailCommand Decode(
            RailResource resource,
            RailBitBuffer buffer)
        {
            RailCommand command = Create(resource);

            // Read: [SenderTick]
            command.ClientTick = buffer.ReadTick();

            // Read: [Command Data]
            command.DecodeData(buffer);

            return command;
        }
#endif

        #endregion
    }

    /// <summary>
    ///     This is the class to override to attach user-defined data to an entity.
    /// </summary>
    public abstract class RailCommand<T> : RailCommand
        where T : RailCommand<T>, new()
    {
        #region Casting Overrides

        protected override void SetDataFrom(RailCommand other)
        {
            SetDataFrom((T) other);
        }

        #endregion

        protected abstract void SetDataFrom(T other);
    }
}