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
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic.State
{
    public abstract class RailState<T> : RailState
        where T : RailState<T>
    {
        public abstract void ApplyMutableFrom(T source, uint flags);
        public abstract void ApplyControllerFrom(T source);
        public abstract void ApplyImmutableFrom(T source);

        public abstract uint CompareMutableData(T other);
        public abstract bool IsControllerDataEqual(T other);

        #region Casting Overrides
        public override void ApplyMutableFrom(RailState source, uint flags)
        {
            ApplyMutableFrom((T) source, flags);
        }

        public override void ApplyControllerFrom(RailState source)
        {
            ApplyControllerFrom((T) source);
        }

        public override void ApplyImmutableFrom(RailState source)
        {
            ApplyImmutableFrom((T) source);
        }

        public override uint CompareMutableData(RailState other)
        {
            return CompareMutableData((T) other);
        }

        public override bool IsControllerDataEqual(RailState other)
        {
            return IsControllerDataEqual((T) other);
        }
        #endregion
    }

    /// <summary>
    ///     States are the fundamental data management class of Railgun. They
    ///     contain all of the synchronized information that an Entity needs to
    ///     function. States have multiple sub-fields that are sent at different
    ///     cadences, as follows:
    ///     Mutable Data:
    ///     Sent whenever the state differs from the client's view.
    ///     Delta-encoded against the client's view.
    ///     Controller Data:
    ///     Sent to the controller of the entity every update.
    ///     Not delta-encoded -- always sent full-encode.
    ///     Immutable Data:
    ///     Sent only once at creation. Can not be changed after.
    ///     Removal Data (Not currently implemented):
    ///     Sent when the state is removed. Arrives at the time of removal.
    /// </summary>
    public abstract class RailState : IRailPoolable<RailState>
    {
        private const uint FLAGS_ALL = 0xFFFFFFFF; // All values different
        private const uint FLAGS_NONE = 0x00000000; // No values different

        private int factoryType;

        protected abstract int FlagBits { get; }

        private uint Flags { get; set; } // Synchronized
        public bool HasControllerData { get; private set; } // Synchronized
        public bool HasImmutableData { get; private set; } // Synchronized

        public virtual void InitializeData()
        {
        }

        public abstract void ResetAllData();
        public abstract void ResetControllerData();

        public abstract void ApplyMutableFrom(RailState source, uint flags);
        public abstract void ApplyControllerFrom(RailState source);
        public abstract void ApplyImmutableFrom(RailState source);

        public abstract uint CompareMutableData(RailState basis);
        public abstract bool IsControllerDataEqual(RailState basis);

        protected static bool GetFlag(uint flags, uint flag)
        {
            return (flags & flag) == flag;
        }

        protected static uint SetFlag<T>(bool isEqual, uint flag)
        {
            return isEqual ? FLAGS_NONE : flag;
        }

        public RailEntity ProduceEntity(RailResource resource)
        {
            return RailEntity.Create(resource, factoryType);
        }

        public RailState Clone(IRailStateConstruction stateCreator)
        {
            RailState clone = Create(stateCreator, factoryType);
            clone.OverwriteFrom(this);
            return clone;
        }

        public void OverwriteFrom(RailState source)
        {
            Flags = source.Flags;
            ApplyMutableFrom(source, FLAGS_ALL);
            ApplyControllerFrom(source);
            ApplyImmutableFrom(source);
            HasControllerData = source.HasControllerData;
            HasImmutableData = source.HasImmutableData;
        }

        private void Reset()
        {
            Flags = 0;
            HasControllerData = false;
            HasImmutableData = false;
            ResetAllData();
        }

        [OnlyIn(Component.Client)]
        public void ApplyDelta(RailStateDelta delta)
        {
            RailState deltaState = delta.State;
            ApplyMutableFrom(deltaState, deltaState.Flags);

            ResetControllerData();
            if (deltaState.HasControllerData) ApplyControllerFrom(deltaState);
            HasControllerData = delta.HasControllerData;

            HasImmutableData = delta.HasImmutableData || HasImmutableData;
            if (deltaState.HasImmutableData) ApplyImmutableFrom(deltaState);
        }

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
                buffer.WriteInt(stateCreator.EntityTypeCompressor, state.factoryType);

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
                state = Create(stateCreator, factoryType);

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

        #region Pooling
        IRailMemoryPool<RailState> IRailPoolable<RailState>.Pool { get; set; }

        void IRailPoolable<RailState>.Reset()
        {
            Reset();
        }
        #endregion

        #region Creation
        public static RailState Create(IRailStateConstruction creator, int factoryType)
        {
            RailState state = creator.CreateState(factoryType);
            state.factoryType = factoryType;
            state.InitializeData();
            return state;
        }

        /// <summary>
        ///     Creates a delta between a state and a record. If forceUpdate is set
        ///     to false, this function will return null if there is no change between
        ///     the current and basis.
        /// </summary>
        [OnlyIn(Component.Server)]
        public static RailStateDelta CreateDelta(
            IRailStateConstruction stateCreator,
            EntityId entityId,
            RailState current,
            IEnumerable<RailStateRecord> basisStates,
            bool includeControllerData,
            bool includeImmutableData,
            Tick commandAck,
            Tick removedTick,
            bool forceAllMutable)
        {
            bool shouldReturn = forceAllMutable ||
                                includeControllerData ||
                                includeImmutableData ||
                                removedTick.IsValid;

            // We don't know what the client has and hasn't received from us since
            // the acked state. As a result, we'll build diff flags across all 
            // states sent *between* the latest and current. This accounts for
            // situations where a value changes and then quickly changes back,
            // while appearing as no change on just the current-latest diff.
            uint flags = 0;
            if (forceAllMutable == false && basisStates != null)
            {
                foreach (RailStateRecord record in basisStates)
                {
                    flags |= current.CompareMutableData(record.State);
                }
            }
            else
            {
                flags = FLAGS_ALL;
            }

            if (flags == FLAGS_NONE && shouldReturn == false) return null;

            RailState deltaState = Create(stateCreator, current.factoryType);
            deltaState.Flags = flags;
            deltaState.ApplyMutableFrom(current, deltaState.Flags);

            deltaState.HasControllerData = includeControllerData;
            if (includeControllerData) deltaState.ApplyControllerFrom(current);

            deltaState.HasImmutableData = includeImmutableData;
            if (includeImmutableData) deltaState.ApplyImmutableFrom(current);

            // We don't need to include a tick when sending -- it's in the packet
            RailStateDelta delta = stateCreator.CreateDelta();
            delta.Initialize(Tick.INVALID, entityId, deltaState, removedTick, commandAck, false);
            return delta;
        }

        /// <summary>
        ///     Creates a record of the current state, taking the latest record (if
        ///     any) into account. If a latest state is given, this function will
        ///     return null if there is no change between the current and latest.
        /// </summary>
        public static RailStateRecord CreateRecord(
            IRailStateConstruction stateCreator,
            Tick tick,
            RailState current,
            RailStateRecord latestRecord = null)
        {
            if (latestRecord != null)
            {
                RailState latest = latestRecord.State;
                bool shouldReturn = current.CompareMutableData(latest) > 0 ||
                                    current.IsControllerDataEqual(latest) == false;
                if (shouldReturn == false) return null;
            }

            RailStateRecord record = stateCreator.CreateRecord();
            record.Overwrite(stateCreator, tick, current);
            return record;
        }
        #endregion

        #region Client
        public abstract void DecodeMutableData(RailBitBuffer buffer, uint flags);
        public abstract void DecodeControllerData(RailBitBuffer buffer);
        public abstract void DecodeImmutableData(RailBitBuffer buffer);
        #endregion

        #region Server
        public abstract void EncodeMutableData(RailBitBuffer buffer, uint flags);
        public abstract void EncodeControllerData(RailBitBuffer buffer);
        public abstract void EncodeImmutableData(RailBitBuffer buffer);
        #endregion
    }
}
