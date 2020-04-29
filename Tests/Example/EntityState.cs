using System;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;

namespace Tests.Example
{
    public class EntityState : RailState<EntityState>
    {
        public int EntityId;
        public float PosX;
        public float PosY;
        protected override void ResetAllData()
        {
            EntityId = 0;
            PosX = 0.0f;
            PosY = 0.0f;
        }

        #region Mutable
        [Flags]
        private enum Flags : uint
        {
            None = 0x00,
            PosX = 0x01,
            PosY = 0x02,
            All = PosX | PosY
        }

        protected override int FlagBits
        {
            get
            {
                return (int)Flags.All;
            }
        }
        protected override void ApplyMutableFrom(EntityState source, uint uFlags)
        {
            Flags flag = (Flags)uFlags;
            if (flag.HasFlag(Flags.PosX)) PosX = source.PosX;
            if (flag.HasFlag(Flags.PosY)) PosY = source.PosY;
        }

        protected override uint CompareMutableData(EntityState other)
        {

            return (uint)((GameMath.CoordinatesEqual(PosX, other.PosX) ? Flags.None : Flags.PosX) |
                           (GameMath.CoordinatesEqual(PosY, other.PosY) ? Flags.None : Flags.PosY));
        }
        protected override void DecodeMutableData(RailBitBuffer buffer, uint uFlags)
        {
            Flags flag = (Flags)uFlags;
            if (flag.HasFlag(Flags.PosX)) PosX = buffer.ReadFloat(Compression.Coordinate);
            if (flag.HasFlag(Flags.PosY)) PosY = buffer.ReadFloat(Compression.Coordinate);
        }
        protected override void EncodeMutableData(RailBitBuffer buffer, uint uFlags)
        {
            Flags flag = (Flags)uFlags;
            if (flag.HasFlag(Flags.PosX)) buffer.WriteFloat(Compression.Coordinate, PosX);
            if (flag.HasFlag(Flags.PosY)) buffer.WriteFloat(Compression.Coordinate, PosY);
        }
        #endregion
        #region Immutable
        protected override void ApplyImmutableFrom(EntityState source)
        {
            EntityId = source.EntityId;
        }
        protected override void DecodeImmutableData(RailBitBuffer buffer)
        {
            EntityId = buffer.ReadInt();
            
        }
        protected override void EncodeImmutableData(RailBitBuffer buffer)
        {
            buffer.WriteInt(EntityId);
        }
        #endregion
        #region Controller
        protected override void ApplyControllerFrom(EntityState source)
        {
        }
        protected override void DecodeControllerData(RailBitBuffer buffer)
        {
        }
        protected override void EncodeControllerData(RailBitBuffer buffer)
        {
        }
        protected override bool IsControllerDataEqual(EntityState basis)
        {
            return true;
        }
        protected override void ResetControllerData()
        {
        }
        #endregion
    }
}
