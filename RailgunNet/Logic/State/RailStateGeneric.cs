using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using RailgunNet.System.Encoding;

namespace RailgunNet.Logic.State
{
    public class RailStateGeneric<T> : RailState<RailStateGeneric<T>>
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

        [PublicAPI]
        public T Data { get; }
        protected override int FlagBits => mutable.Count;

        private static uint ToFlag(int index)
        {
            return (uint) 0x1 << index;
        }

        public override void ApplyControllerFrom(RailStateGeneric<T> source)
        {
            for (int i = 0; i < controller.Count; ++i)
            {
                controller[i].ApplyFrom(source.controller[i]);
            }
        }

        public override void ApplyImmutableFrom(RailStateGeneric<T> source)
        {
            for (int i = 0; i < immutable.Count; ++i)
            {
                immutable[i].ApplyFrom(source.immutable[i]);
            }
        }

        public override void ApplyMutableFrom(RailStateGeneric<T> source, uint flags)
        {
            for (int i = 0; i < mutable.Count; ++i)
            {
                if ((flags & ToFlag(i)) == ToFlag(i))
                {
                    mutable[i].ApplyFrom(source.mutable[i]);
                }
            }
        }

        public override uint CompareMutableData(RailStateGeneric<T> other)
        {
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

        public override bool IsControllerDataEqual(RailStateGeneric<T> other)
        {
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
    }
}
