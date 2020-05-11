using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    public class RailSynchronized<TContainer> : IRailSynchronized
    {
        private readonly object compressor;
        private readonly Func<RailBitBuffer, object> decode;
        private readonly Action<RailBitBuffer, object> encode;

        private readonly Func<TContainer, object> getter;
        private readonly object initialValue;
        private readonly TContainer instance;
        private readonly Action<TContainer, object> setter;

        public RailSynchronized(TContainer instanceToWrap, MemberInfo member)
        {
            instance = instanceToWrap;
            getter = InvokableFactory.CreateUntypedGetter<TContainer>(member);
            setter = InvokableFactory.CreateUntypedSetter<TContainer>(member);
            initialValue = getter(instance);
            Type underlyingType = member.GetUnderlyingType();

            CompressorAttribute att = member.GetCustomAttribute<CompressorAttribute>();
            if (att == null)
            {
                encode = InvokableFactory.CreateCall<RailBitBuffer>(
                    GetEncodeMethod(typeof(RailBitBuffer), underlyingType));
                decode = InvokableFactory.CreateCallWithReturn<RailBitBuffer>(
                    GetDecodeMethod(typeof(RailBitBuffer), underlyingType));
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
                    GetEncodeMethod(compressor.GetType(), underlyingType),
                    compressor);
                decode = InvokableFactory.CreateCallWithReturn<RailBitBuffer>(
                    GetDecodeMethod(compressor.GetType(), underlyingType),
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
            RailSynchronized<TContainer> other = 
                (RailSynchronized<TContainer>) from;
            setter(instance, getter(other.instance));
        }

        public bool Equals(IRailSynchronized from)
        {
            RailSynchronized<TContainer> other =
                (RailSynchronized<TContainer>) from;
            return getter(instance).Equals(getter(other.instance));
        }

        public void Reset()
        {
            setter(instance, initialValue);
        }

        private static MethodInfo GetEncodeMethod(Type encoder, Type toBeEncoded)
        {
            foreach (MethodInfo method in encoder.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                EncoderAttribute att = method.GetCustomAttribute<EncoderAttribute>();
                if (att != null)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == toBeEncoded ||
                        parameters.Length > 1 && parameters[1].ParameterType == toBeEncoded)
                    {
                        return method;
                    }
                }
            }

            if (RailSynchronizedFactory.Encoders.TryGetValue(toBeEncoded, out MethodInfo encoderMethod))
            {
                return encoderMethod;
            }

            throw new ArgumentException(
                $"Cannot find an encoder method for type {toBeEncoded}.");
        }

        private static MethodInfo GetDecodeMethod(Type decoder, Type toBeDecoded)
        {
            foreach (MethodInfo method in decoder.GetMethods())
            {
                DecoderAttribute att = method.GetCustomAttribute<DecoderAttribute>();
                
                if (att != null && method.ReturnType == toBeDecoded)
                {
                    return method;
                }
            }

            if (RailSynchronizedFactory.Decoders.TryGetValue(toBeDecoded, out MethodInfo decoderMethod))
            {
                return decoderMethod;
            }

            throw new ArgumentException(
                $"Cannot find a decoder method for type {toBeDecoded}.");
        }
    }
}
