using System.Collections.Generic;
using System.Linq;
using RailgunNet.System.Encoding;

namespace RailgunNet.Logic
{
    public interface IRailStateMember
    {
        void WriteTo(RailBitBuffer buffer);
        void ReadFrom(RailBitBuffer buffer);
        void ApplyFrom(IRailStateMember other);
        bool Equals(IRailStateMember other);
        void Reset();
    }
    public class RailStateGeneric : RailState<RailStateGeneric>
    {
        private readonly List<IRailStateMember> mutable;
        private readonly List<IRailStateMember> immutable;
        private readonly List<IRailStateMember> controller;
        public RailStateGeneric(List<IRailStateMember> mutables,
                                List<IRailStateMember> immutables,
                                List<IRailStateMember> controllers)
        {
            mutable = mutables;
            immutable = immutables;
            controller = controllers;
        }
        protected override int FlagBits => mutable.Count;

        private static uint ToFlag(int index)
        {
            return (uint) 0x1 << index;
        }

        public override void ApplyControllerFrom(RailStateGeneric source)
        {
            for (int i = 0; i < controller.Count; ++i)
            {
                controller[i].ApplyFrom(source.controller[i]);
            }
        }

        public override void ApplyImmutableFrom(RailStateGeneric source)
        {
            for (int i = 0; i < immutable.Count; ++i)
            {
                immutable[i].ApplyFrom(source.immutable[i]);
            }
        }

        public override void ApplyMutableFrom(RailStateGeneric source, uint flags)
        {
            for (int i = 0; i < mutable.Count; ++i)
            {
                if ((flags & ToFlag(i)) == ToFlag(i))
                {
                    mutable[i].ApplyFrom(source.mutable[i]);
                }
            }
        }

        public override uint CompareMutableData(RailStateGeneric other)
        {
            uint uiFlags = 0x0;
            for (int i = 0; i < mutable.Count; ++i)
            {
                if(!mutable[i].Equals(other.mutable[i]))
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

        public override bool IsControllerDataEqual(RailStateGeneric other)
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
