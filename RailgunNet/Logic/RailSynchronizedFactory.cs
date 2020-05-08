using System;
using System.Reflection;
using RailgunNet.System.Types;
using RailgunNet.Util;

namespace RailgunNet.Logic
{
    public static class RailSynchronizedFactory
    {
        public static IRailSynchronized Create<T>(T instance, MemberInfo info)
        {
            Type t = info.GetUnderlyingType();
            if (t == typeof(byte))
            {
                return new RailSynchronized<T, byte>(instance, info);
            }

            if (t == typeof(uint))
            {
                return new RailSynchronized<T, uint>(instance, info);
            }

            if (t == typeof(int))
            {
                return new RailSynchronized<T, int>(instance, info);
            }

            if (t == typeof(bool))
            {
                return new RailSynchronized<T, bool>(instance, info);
            }

            if (t == typeof(ushort))
            {
                return new RailSynchronized<T, ushort>(instance, info);
            }

            if (t == typeof(string))
            {
                return new RailSynchronized<T, string>(instance, info);
            }

            if (t == typeof(float))
            {
                return new RailSynchronized<T, float>(instance, info);
            }

            if (t == typeof(EntityId))
            {
                return new RailSynchronized<T, EntityId>(instance, info);
            }

            throw new ArgumentException("Member type not supported.", nameof(info));
        }
    }
}
