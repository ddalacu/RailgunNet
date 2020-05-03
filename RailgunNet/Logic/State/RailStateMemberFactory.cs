using System;
using System.Reflection;
using RailgunNet.System.Encoding;
using RailgunNet.Util;

namespace RailgunNet.Logic.State
{
    public interface IRailStateMember
    {
        void WriteTo(RailBitBuffer buffer);
        void ReadFrom(RailBitBuffer buffer);
        void ApplyFrom(IRailStateMember other);
        bool Equals(IRailStateMember other);
        void Reset();
    }

    public static class RailStateMemberFactory
    {
        public static IRailStateMember Create<T>(T instance, MemberInfo info)
        {
            Type t = info.GetUnderlyingType();
            if (t == typeof(byte))
            {
                return new RailStateMember<T, byte>(instance, info);
            }

            if (t == typeof(uint))
            {
                return new RailStateMember<T, uint>(instance, info);
            }

            if (t == typeof(int))
            {
                return new RailStateMember<T, int>(instance, info);
            }

            if (t == typeof(bool))
            {
                return new RailStateMember<T, bool>(instance, info);
            }

            if (t == typeof(ushort))
            {
                return new RailStateMember<T, ushort>(instance, info);
            }

            if (t == typeof(string))
            {
                return new RailStateMember<T, string>(instance, info);
            }

            if (t == typeof(float))
            {
                return new RailStateMember<T, float>(instance, info);
            }

            throw new ArgumentException("Member type not supported.", nameof(info));
        }
    }
}
