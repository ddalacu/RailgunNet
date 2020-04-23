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

using RailgunNet.Factory;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    public enum RailPolicy
    {
        All,
#if SERVER
        NoProxy,
#endif
#if CLIENT
        NoFrozen,
#endif
    }

    /// <summary>
    ///     Events are sent attached to entities and represent temporary changes
    ///     in status. They can be sent to specific controllers or broadcast to all
    ///     controllers for whom the entity is in scope.
    /// </summary>
    public abstract class RailEvent
        : IRailPoolable<RailEvent>
    {
        private int factoryType;

        /// <summary>
        ///     Whether or not this event can be sent to a frozen entity.
        ///     TODO: NOT FULLY IMPLEMENTED
        /// </summary>
        protected virtual bool CanSendToFrozen => false;

        /// <summary>
        ///     Whether or not this event can be sent by someone other than the
        ///     controller of the entity. Ignored on clients.
        /// </summary>
        protected virtual bool CanProxySend => false;

        // Synchronized
        public SequenceId EventId { get; set; }

        // Local only
        public int Attempts { get; set; }

        private RailRoom Room { get; set; }
        private RailController Sender { get; set; }

        public static TEvent Create<TEvent>(RailResource resource)
            where TEvent : RailEvent
        {
            int factoryType = resource.GetEventFactoryType<TEvent>();
            TEvent evnt = (TEvent) Create(resource, factoryType);
            return evnt;
        }

        private static RailEvent Create(IRailEventCreator creator, int factoryType)
        {
            RailEvent evnt = creator.CreateEvent(factoryType);
            evnt.factoryType = factoryType;
            return evnt;
        }

        public TEntity Find<TEntity>(
            EntityId id,
            RailPolicy policy = RailPolicy.All)
            where TEntity : class, IRailEntity
        {
            if (Room == null)
                return null;
            if (id.IsValid == false)
                return null;
#if SERVER
            if (policy == RailPolicy.NoProxy && Sender == null)
                return null;
#endif

            if (Room.TryGet(id, out IRailEntity entity) == false)
                return null;
#if CLIENT
            if (policy == RailPolicy.NoFrozen && entity.IsFrozen)
                return null;
#endif
#if SERVER
            if (policy == RailPolicy.NoProxy && entity.Controller != Sender)
                return null;
#endif
            if (entity is TEntity cast)
                return cast;
            return null;
        }

        protected abstract void SetDataFrom(RailEvent other);

        protected abstract void EncodeData(RailBitBuffer buffer, Tick packetTick);
        protected abstract void DecodeData(RailBitBuffer buffer, Tick packetTick);
        protected abstract void ResetData();

        protected virtual bool Validate()
        {
            return true;
        }

        protected virtual void Execute(
            RailRoom room,
            RailController sender)
        {
            // Override this to process events
        }

        public void Free()
        {
            RailPool.Free(this);
        }

        public RailEvent Clone(RailResource resource)
        {
            RailEvent clone = Create(resource, factoryType);
            clone.EventId = EventId;
            clone.Attempts = Attempts;
            clone.Room = Room;
            clone.Sender = Sender;
            clone.SetDataFrom(this);
            return clone;
        }

        protected virtual void Reset()
        {
            EventId = SequenceId.Invalid;
            Attempts = 0;
            Room = null;
            Sender = null;
            ResetData();
        }

        public void Invoke(
            RailRoom room,
            RailController sender)
        {
            Room = room;
            Sender = sender;
            if (Validate())
                Execute(room, sender);
        }

        public void RegisterSent()
        {
            if (Attempts > 0)
                Attempts--;
        }

        public void RegisterSkip()
        {
            RegisterSent();
        }

        #region Pooling

        IRailMemoryPool<RailEvent> IRailPoolable<RailEvent>.Pool { get; set; }

        void IRailPoolable<RailEvent>.Reset()
        {
            Reset();
        }

        #endregion

        #region Encode/Decode/etc.

        /// <summary>
        ///     Note that the packetTick may not be the tick this event was created on
        ///     if we're re-trying to send this event in subsequent packets. This tick
        ///     is intended for use in tick diffs for compression.
        /// </summary>
        public void Encode(
            RailIntCompressor compressor,
            RailBitBuffer buffer,
            Tick packetTick)
        {
            // Write: [EventType]
            buffer.WriteInt(compressor, factoryType);

            // Write: [EventId]
            buffer.WriteSequenceId(EventId);

            // Write: [EventData]
            EncodeData(buffer, packetTick);
        }

        /// <summary>
        ///     Note that the packetTick may not be the tick this event was created on
        ///     if we're re-trying to send this event in subsequent packets. This tick
        ///     is intended for use in tick diffs for compression.
        /// </summary>
        public static RailEvent Decode(
            IRailEventCreator creator,
            RailIntCompressor compressor,
            RailBitBuffer buffer,
            Tick packetTick)
        {
            // Read: [EventType]
            int factoryType = buffer.ReadInt(compressor);

            RailEvent evnt = Create(creator, factoryType);

            // Read: [EventId]
            evnt.EventId = buffer.ReadSequenceId();

            // Read: [EventData]
            evnt.DecodeData(buffer, packetTick);

            return evnt;
        }

        #endregion
    }

    /// <summary>
    ///     This is the class to override to attach user-defined data to an entity.
    /// </summary>
    public abstract class RailEvent<TDerived> : RailEvent
        where TDerived : RailEvent<TDerived>, new()
    {
        #region Casting Overrides

        protected override void SetDataFrom(RailEvent other)
        {
            SetDataFrom((TDerived) other);
        }

        #endregion

        protected abstract void SetDataFrom(TDerived other);
    }
}