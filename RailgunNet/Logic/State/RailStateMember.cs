using System;
using System.Reflection;
using RailgunNet.System.Encoding;
using RailgunNet.Util;

namespace RailgunNet.Logic.State
{
    public class RailStateMember<TContainer, TValue> : IRailStateMember
    {
        private readonly object compressor;
        private readonly Func<RailBitBuffer, object> decode;
        private readonly Action<RailBitBuffer, object> encode;

        private readonly Func<TContainer, object> getter;
        private readonly TValue initialValue;
        private readonly TContainer instance;
        private readonly Action<TContainer, object> setter;

        public RailStateMember(TContainer instanceToWrap, MemberInfo member)
        {
            instance = instanceToWrap;
            getter = FastInvoke.BuildUntypedGetter<TContainer>(member);
            setter = FastInvoke.BuildUntypedSetter<TContainer>(member);
            initialValue = (TValue) getter(instance);

            CompressorAttribute att = member.GetCustomAttribute<CompressorAttribute>();
            if (att == null)
            {
                encode = FastInvoke.BuildEncodeCall(
                    GetEncodeMethod(typeof(RailBitBuffer), typeof(TValue)));
                decode = FastInvoke.BuildDecodeCall(
                    GetDecodeMethod(typeof(RailBitBuffer), typeof(TValue)));
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
            RailStateMember<TContainer, TValue> other = (RailStateMember<TContainer, TValue>) from;
            setter(instance, getter(other.instance));
        }

        public bool Equals(IRailStateMember from)
        {
            RailStateMember<TContainer, TValue> other = (RailStateMember<TContainer, TValue>) from;
            return getter(instance).Equals(getter(other.instance));
        }

        public void Reset()
        {
            setter(instance, initialValue);
        }

        private static MethodInfo GetEncodeMethod(Type encoder, Type value)
        {
            Encoders.SupportedType eType = Encoders.ToSupportedType(value);
            foreach (MethodInfo method in encoder.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                EncoderAttribute att = method.GetCustomAttribute<EncoderAttribute>();
                if (att != null && att.Type == eType)
                {
                    return method;
                }
            }

            throw new ArgumentException(
                $"{encoder} does not contain an encoder method for value type {value}.");
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

            throw new ArgumentException(
                $"{decoder} does not contain a decoder method for value type {value}.");
        }
    }
}
