using System;
using System.Collections.Generic;
using System.Reflection;
using RailgunNet.Logic.State;
using RailgunNet.System.Encoding;
using RailgunNet.Util;

namespace RailgunNet.Logic
{
    public static class RailStateGenericFactory
    {
        public static RailStateGeneric Create<T>(T data)
        {
            List<IRailStateMember> mutables = new List<IRailStateMember>();
            List<IRailStateMember> immutables = new List<IRailStateMember>();
            List<IRailStateMember> controllers = new List<IRailStateMember>();
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                if (Attribute.IsDefined(prop, typeof(MutableAttribute)))
                {
                    mutables.Add(CreateMember<T>(data, prop));
                }
                else if (Attribute.IsDefined(prop, typeof(ImmutableAttribute)))
                {
                    immutables.Add(CreateMember<T>(data, prop));
                }
                else if (Attribute.IsDefined(prop, typeof(ControllerAttribute)))
                {
                    controllers.Add(CreateMember<T>(data, prop));
                }
            }
            return new RailStateGeneric(mutables, immutables, controllers);
        }

        private static IRailStateMember CreateMember<T>(T instance, MemberInfo info)
        {
            Type t = info.GetUnderlyingType();
            if (t == typeof(byte))
            {
                return new RailStateMember<T, byte>(instance, info);
            }
            else if (t == typeof(uint))
            {
                return new RailStateMember<T, uint>(instance, info);
            }
            else if (t == typeof(int))
            {
                return new RailStateMember<T, int>(instance, info);
            }
            else if (t == typeof(bool))
            {
                return new RailStateMember<T, bool>(instance, info);
            }
            else if (t == typeof(ushort))
            {
                return new RailStateMember<T, ushort>(instance, info);
            }
            else if (t == typeof(string))
            {
                return new RailStateMember<T, string>(instance, info);
            }
            else if (t == typeof(float))
            {
                return new RailStateMember<T, float>(instance, info);
            }
            throw new ArgumentException("Member type not supported.", nameof(info));
        }
        private class RailStateMember<TContainer, TValue> : IRailStateMember
        {
            private readonly TValue initialValue;
            private readonly TContainer instance;

            private readonly Func<TContainer, object> getter;
            private readonly Action<TContainer, object> setter;

            private readonly object compressor = null;
            private readonly Func<RailBitBuffer, object> decode;
            private readonly Action<RailBitBuffer, object> encode;

            public RailStateMember(TContainer instanceToWrap, MemberInfo member)
            {
                instance = instanceToWrap;
                getter = FastInvoke.BuildUntypedGetter<TContainer>(member);
                setter = FastInvoke.BuildUntypedSetter<TContainer>(member);
                initialValue = (TValue)getter(instance);

                CompressorAttribute att = member.GetCustomAttribute<CompressorAttribute>();
                if (att == null)
                {
                    encode = FastInvoke.BuildEncodeCall(GetEncodeMethod(typeof(RailBitBuffer), typeof(TValue)));
                    decode = FastInvoke.BuildDecodeCall(GetDecodeMethod(typeof(RailBitBuffer), typeof(TValue)));
                }
                else
                {
                    compressor = att.Compressor.GetConstructor(Type.EmptyTypes).Invoke(null);
                    encode = FastInvoke.BuildEncodeCall(
                        GetEncodeMethod(compressor.GetType(), typeof(TValue)),
                        compressor);
                    decode = FastInvoke.BuildDecodeCall(
                        GetDecodeMethod(compressor.GetType(), typeof(TValue)),
                        compressor);
                }
            }
            public void ReadFrom(RailBitBuffer buffer)
            {
                setter(instance, decode(buffer));
            }

            public void WriteTo(RailBitBuffer buffer)
            {
                encode(buffer, getter(instance));
            }
            public void ApplyFrom(IRailStateMember from)
            {
                var other = (RailStateMember<TContainer, TValue>) from;
                setter(instance, getter(other.instance));
            }

            public bool Equals(IRailStateMember from)
            {
                var other = (RailStateMember<TContainer, TValue>) from;
                return getter(instance).Equals(getter(other.instance));
            }

            public void Reset()
            {
                setter(instance, initialValue);
            }
            private static MethodInfo GetEncodeMethod(Type encoder, Type value)
            {
                Encoders.SupportedType eType = Encoders.ToSupportedType(value);
                foreach (MethodInfo method in encoder.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    EncoderAttribute att = method.GetCustomAttribute<EncoderAttribute>();
                    if (att != null && att.Type == eType)
                    {
                        return method;
                    }
                }
                throw new ArgumentException($"{encoder} does not contain an encoder method for value type {value}.");
            }
            private static MethodInfo GetDecodeMethod(Type decoder, Type value)
            {
                Encoders.SupportedType eType = Encoders.ToSupportedType(value);
                foreach (MethodInfo method in decoder.GetMethods())
                {
                    DecoderAttribute att = method.GetCustomAttribute<DecoderAttribute>();
                    if (att != null && att.Type == eType)
                    {
                        return method;
                    }
                }
                throw new ArgumentException($"{decoder} does not contain a decoder method for value type {value}.");
            }
        }
    }
}
