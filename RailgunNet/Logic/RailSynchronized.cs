using System;
using System.Reflection;
using RailgunNet.System.Encoding;
using RailgunNet.Util;

namespace RailgunNet.Logic
{
    public interface IRailSynchronized
    {
        void WriteTo(RailBitBuffer buffer);
        void ReadFrom(RailBitBuffer buffer);
        void ApplyFrom(IRailSynchronized other);
        bool Equals(IRailSynchronized other);
        void Reset();
    }

    public class RailSynchronized<TContainer, TValue> : IRailSynchronized
    {
        private readonly object compressor;
        private readonly Func<RailBitBuffer, object> decode;
        private readonly Action<RailBitBuffer, object> encode;

        private readonly Func<TContainer, object> getter;
        private readonly TValue initialValue;
        private readonly TContainer instance;
        private readonly Action<TContainer, object> setter;

        public RailSynchronized(TContainer instanceToWrap, MemberInfo member)
        {
            instance = instanceToWrap;
            getter = InvokableFactory.CreateUntypedGetter<TContainer>(member);
            setter = InvokableFactory.CreateUntypedSetter<TContainer>(member);
            initialValue = (TValue) getter(instance);

            CompressorAttribute att = member.GetCustomAttribute<CompressorAttribute>();
            if (att == null)
            {
                encode = InvokableFactory.CreateCall<RailBitBuffer>(
                    GetEncodeMethod(typeof(RailBitBuffer), typeof(TValue)));
                decode = InvokableFactory.CreateCallWithReturn<RailBitBuffer>(
                    GetDecodeMethod(typeof(RailBitBuffer), typeof(TValue)));
            }
            else
            {
                compressor = att.Compressor.GetConstructor(Type.EmptyTypes).Invoke(null);
                if (compressor == null)
                {
                    throw new ArgumentException(
                        "The declared compressor needs to implement a parameterless default constructor.",
                        nameof(member));
                }

                encode = InvokableFactory.CreateCall<RailBitBuffer>(
                    GetEncodeMethod(compressor.GetType(), typeof(TValue)),
                    compressor);
                decode = InvokableFactory.CreateCallWithReturn<RailBitBuffer>(
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

        public void ApplyFrom(IRailSynchronized from)
        {
            RailSynchronized<TContainer, TValue> other =
                (RailSynchronized<TContainer, TValue>) from;
            setter(instance, getter(other.instance));
        }

        public bool Equals(IRailSynchronized from)
        {
            RailSynchronized<TContainer, TValue> other =
                (RailSynchronized<TContainer, TValue>) from;
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
