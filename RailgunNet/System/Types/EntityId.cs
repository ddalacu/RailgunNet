using System.Collections.Generic;
using RailgunNet.System.Encoding;

namespace RailgunNet.System.Types
{
    public static class EntityIdExtensions
    {
        [Encoder]
        public static void WriteEntityId(this RailBitBuffer buffer, EntityId entityId)
        {
            entityId.Write(buffer);
        }

        [Decoder]
        public static EntityId ReadEntityId(this RailBitBuffer buffer)
        {
            return EntityId.Read(buffer);
        }

        public static EntityId PeekEntityId(this RailBitBuffer buffer)
        {
            return EntityId.Peek(buffer);
        }
    }

    public readonly struct EntityId
    {
        #region Encoding/Decoding
        #region Byte Writing
        public int PutBytes(byte[] buffer, int start)
        {
            return RailBitBuffer.PutBytes(idValue, buffer, start);
        }

        public static EntityId ReadBytes(byte[] buffer, ref int position)
        {
            return new EntityId(RailBitBuffer.ReadBytes(buffer, ref position));
        }
        #endregion

        public void Write(RailBitBuffer buffer)
        {
            buffer.WriteUInt(idValue);
        }

        public static EntityId Read(RailBitBuffer buffer)
        {
            return new EntityId(buffer.ReadUInt());
        }

        public static EntityId Peek(RailBitBuffer buffer)
        {
            return new EntityId(buffer.PeekUInt());
        }
        #endregion

        private class EntityIdComparer : IEqualityComparer<EntityId>
        {
            public bool Equals(EntityId x, EntityId y)
            {
                return x.idValue == y.idValue;
            }

            public int GetHashCode(EntityId x)
            {
                return (int)x.idValue;
            }
        }

        /// <summary>
        ///     An invalid entity ID. Should never be used explicitly.
        /// </summary>
        public static readonly EntityId INVALID = new EntityId(0);

        public static readonly EntityId START = new EntityId(1);

        public static readonly IEqualityComparer<EntityId> ComparerInstance = new EntityIdComparer();

        public static bool operator ==(EntityId a, EntityId b)
        {
            return a.idValue == b.idValue;
        }

        public static bool operator !=(EntityId a, EntityId b)
        {
            return a.idValue != b.idValue;
        }

        public bool IsValid => idValue > 0;

        private readonly uint idValue;

        private EntityId(uint idValue)
        {
            this.idValue = idValue;
        }

        public EntityId GetNext()
        {
            return new EntityId(idValue + 1);
        }

        public override int GetHashCode()
        {
            return (int)idValue;
        }

        public override bool Equals(object obj)
        {
            if (obj is EntityId) return ((EntityId)obj).idValue == idValue;
            return false;
        }

        public override string ToString()
        {
            return "EntityId:" + idValue;
        }
    }
}
