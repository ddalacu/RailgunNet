using System;

namespace RailgunNet.System.Encoding
{
    public static class Encoders
    {
        public enum SupportedType
        {
            Byte_t,
            UInt_t,
            Int_t,
            Bool_t,
            UShort_t,
            StringAscii_t,
            Float_t // Requires RailFloatCompressor
        }

        public static SupportedType ToSupportedType(Type t)
        {
            if (t == typeof(byte))
            {
                return SupportedType.Byte_t;
            }

            if (t == typeof(uint))
            {
                return SupportedType.UInt_t;
            }

            if (t == typeof(int))
            {
                return SupportedType.Int_t;
            }

            if (t == typeof(bool))
            {
                return SupportedType.Byte_t;
            }

            if (t == typeof(ushort))
            {
                return SupportedType.UShort_t;
            }

            if (t == typeof(string))
            {
                return SupportedType.StringAscii_t;
            }

            if (t == typeof(float))
            {
                return SupportedType.Float_t;
            }

            throw new ArgumentException("Unknown type.", nameof(t));
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class EncoderAttribute : Attribute
    {
        public EncoderAttribute(Encoders.SupportedType eType)
        {
            Type = eType;
        }

        public Encoders.SupportedType Type { get; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class DecoderAttribute : Attribute
    {
        public DecoderAttribute(Encoders.SupportedType eType)
        {
            Type = eType;
        }

        public Encoders.SupportedType Type { get; }
    }
}
