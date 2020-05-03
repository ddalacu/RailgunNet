using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using RailgunNet.System.Encoding;

namespace RailgunNet.Logic.State
{
    public class RailStateGeneric<T> : RailState
        where T : class, new()
    {
        private readonly List<IRailStateMember> controller = new List<IRailStateMember>();
        private readonly List<IRailStateMember> immutable = new List<IRailStateMember>();
        private readonly List<IRailStateMember> mutable = new List<IRailStateMember>();

        public RailStateGeneric() : this(null)
        {
        }

        public RailStateGeneric(T instance)
        {
            Data = instance ?? new T();
            foreach (PropertyInfo prop in typeof(T).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(prop, typeof(MutableAttribute)))
                {
                    mutable.Add(RailStateMemberFactory.Create(Data, prop));
                }
                else if (Attribute.IsDefined(prop, typeof(ImmutableAttribute)))
                {
                    immutable.Add(RailStateMemberFactory.Create(Data, prop));
                }
                else if (Attribute.IsDefined(prop, typeof(ControllerAttribute)))
                {
                    controller.Add(RailStateMemberFactory.Create(Data, prop));
                }
            }
        }

        [PublicAPI] public T Data { get; }

        private static uint ToFlag(int index)
        {
            return (uint) 0x1 << index;
        }

        #region Interface
        public override int FlagBits => mutable.Count;

        public override void ApplyControllerFrom(RailState sourceBase)
        {
            RailStateGeneric<T> source = (RailStateGeneric<T>) sourceBase;
            for (int i = 0; i < controller.Count; ++i)
            {
                controller[i].ApplyFrom(source.controller[i]);
            }
        }

        public override void ApplyImmutableFrom(RailState sourceBase)
        {
            RailStateGeneric<T> source = (RailStateGeneric<T>) sourceBase;
            for (int i = 0; i < immutable.Count; ++i)
            {
                immutable[i].ApplyFrom(source.immutable[i]);
            }
        }

        public override void ApplyMutableFrom(RailState sourceBase, uint flags)
        {
            RailStateGeneric<T> source = (RailStateGeneric<T>) sourceBase;
            for (int i = 0; i < mutable.Count; ++i)
            {
                if ((flags & ToFlag(i)) == ToFlag(i))
                {
                    mutable[i].ApplyFrom(source.mutable[i]);
                }
            }
        }

        public override uint CompareMutableData(RailState otherBase)
        {
            RailStateGeneric<T> other = (RailStateGeneric<T>) otherBase;
            uint uiFlags = 0x0;
            for (int i = 0; i < mutable.Count; ++i)
            {
                if (!mutable[i].Equals(other.mutable[i]))
                {
                    uiFlags |= ToFlag(i);
                }
            }

            return uiFlags;
        }

        public override bool IsControllerDataEqual(RailState otherBase)
        {
            RailStateGeneric<T> other = (RailStateGeneric<T>) otherBase;
            for (int i = 0; i < controller.Count; ++i)
            {
                if (!controller[i].Equals(other.controller[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override void ResetAllData()
        {
            mutable.ForEach(c => c.Reset());
            immutable.ForEach(c => c.Reset());
            controller.ForEach(c => c.Reset());
        }

        public override void ResetControllerData()
        {
            controller.ForEach(c => c.Reset());
        }
        #endregion

        #region Encode & Decode
        public override void DecodeControllerData(RailBitBuffer buffer)
        {
            controller.ForEach(c => c.ReadFrom(buffer));
        }

        public override void DecodeImmutableData(RailBitBuffer buffer)
        {
            immutable.ForEach(i => i.ReadFrom(buffer));
        }

        public override void DecodeMutableData(RailBitBuffer buffer, uint flags)
        {
            for (int i = 0; i < mutable.Count; ++i)
            {
                if ((flags & ToFlag(i)) == ToFlag(i))
                {
                    mutable[i].ReadFrom(buffer);
                }
            }
        }

        public override void EncodeControllerData(RailBitBuffer buffer)
        {
            controller.ForEach(c => c.WriteTo(buffer));
        }

        public override void EncodeImmutableData(RailBitBuffer buffer)
        {
            immutable.ForEach(i => i.WriteTo(buffer));
        }

        public override void EncodeMutableData(RailBitBuffer buffer, uint flags)
        {
            for (int i = 0; i < mutable.Count; ++i)
            {
                if ((flags & ToFlag(i)) == ToFlag(i))
                {
                    mutable[i].WriteTo(buffer);
                }
            }
        }
        #endregion
    }
}
