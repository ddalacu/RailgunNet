using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.Util.Pooling;
using System.Reflection;
using Tests.Example;
using Xunit;

namespace Tests
{
    public class RailStateTest
    {
        private class Data : RailState
        {
            [Mutable] public int MByteProp { get; set; }
            [Mutable] public uint MUIntProp { get; set; }
            [Mutable] public int MIntProp { get; set; }
            [Mutable] public bool MBoolProp { get; set; }
            [Mutable] public ushort MUShortProp { get; set; }
            [Mutable] public string MStringProp { get; set; }
            [Mutable] public ulong MUInt64Prop { get; set; }
            [Mutable] public long MInt64Prop { get; set; }
        }

        private class DataWithCompressor : RailState
        {
            [Mutable]
            [Compressor(typeof(MyIntCompressor))]
            public int CompressedInt { get; set; }

            [Mutable]
            [Compressor(typeof(MyFloatCompressor))]
            public float CompressedFloat { get; set; }

            [Mutable]
            [Compressor(typeof(MyInt64Compressor))]
            public long CompressedInt64 { get; set; }
        }

        private class DataWithCustomField : RailState
        {
            [Mutable] public Foo Data { get; set; }
        }

        private class MyIntCompressor : RailIntCompressor
        {
            public MyIntCompressor() : base(int.MinValue + 100, int.MaxValue - 100)
            {
            }

            [Encoder]
            public void Write(RailBitBuffer buffer, int i)
            {
                buffer.WriteInt(this, i);
            }

            [Decoder]
            public int Read(RailBitBuffer buffer)
            {
                return buffer.ReadInt(this);
            }
        }

        private class MyFloatCompressor : RailFloatCompressor
        {
            public MyFloatCompressor() : base(
                -512.0f,
                512.0f,
                GameMath.COORDINATE_PRECISION / 10.0f)
            {
            }

            [Encoder]
            public void Write(RailBitBuffer buffer, float f)
            {
                buffer.WriteFloat(this, f);
            }

            [Decoder]
            public float Read(RailBitBuffer buffer)
            {
                return buffer.ReadFloat(this);
            }
        }

        private class MyInt64Compressor : RailInt64Compressor
        {
            public MyInt64Compressor() : base(long.MinValue + 100, long.MaxValue - 100)
            {
            }

            [Encoder]
            public void Write(RailBitBuffer buffer, long i)
            {
                buffer.WriteInt64(this, i);
            }

            [Decoder]
            public long Read(RailBitBuffer buffer)
            {
                return buffer.ReadInt64(this);
            }
        }

        [Fact]
        private void ApplyMutableFields()
        {
            // Setup 2 different states
            Data data0 = new Data
            {
                MByteProp = 0,
                MUIntProp = 0,
                MIntProp = 0,
                MBoolProp = false,
                MUShortProp = 0,
                MStringProp = "",
                MUInt64Prop = 0,
                MInt64Prop = 0
            };
            Data data1 = new Data
            {
                MByteProp = 42,
                MUIntProp = uint.MaxValue - 212,
                MIntProp = int.MinValue + 212,
                MBoolProp = true,
                MUShortProp = 45,
                MStringProp = "46",
                MUInt64Prop = ulong.MaxValue - 212,
                MInt64Prop = long.MinValue + 212
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();
            Assert.NotEqual(data1.MByteProp, data0.MByteProp);
            Assert.NotEqual(data1.MUIntProp, data0.MUIntProp);
            Assert.NotEqual(data1.MIntProp, data0.MIntProp);
            Assert.NotEqual(data1.MBoolProp, data0.MBoolProp);
            Assert.NotEqual(data1.MUShortProp, data0.MUShortProp);
            Assert.NotEqual(data1.MStringProp, data0.MStringProp);
            Assert.NotEqual(data1.MUInt64Prop, data0.MUInt64Prop);
            Assert.NotEqual(data1.MInt64Prop, data0.MInt64Prop);

            // Apply mutable data
            data0.DataSerializer.ApplyMutableFrom(data1.DataSerializer, 0xFFFF);

            // And now they should be equal
            Assert.Equal(data1.MByteProp, data0.MByteProp);
            Assert.Equal(data1.MUIntProp, data0.MUIntProp);
            Assert.Equal(data1.MIntProp, data0.MIntProp);
            Assert.Equal(data1.MBoolProp, data0.MBoolProp);
            Assert.Equal(data1.MUShortProp, data0.MUShortProp);
            Assert.Equal(data1.MStringProp, data0.MStringProp);
            Assert.Equal(data1.MUInt64Prop, data0.MUInt64Prop);
            Assert.Equal(data1.MInt64Prop, data0.MInt64Prop);
        }

        [Fact]
        private void CompareMutableData()
        {
            // Setup 2 identical data objects
            Data data0 = new Data
            {
                MByteProp = 0,
                MUIntProp = 0,
                MIntProp = 0,
                MBoolProp = false,
                MUShortProp = 0,
                MStringProp = "",
                MUInt64Prop = 0,
                MInt64Prop = 0
            };
            Data data1 = new Data
            {
                MByteProp = 0,
                MUIntProp = 0,
                MIntProp = 0,
                MBoolProp = false,
                MUShortProp = 0,
                MStringProp = "",
                MUInt64Prop = 0,
                MInt64Prop = 0
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();
            Assert.Equal(data1.MByteProp, data0.MByteProp);
            Assert.Equal(data1.MUIntProp, data0.MUIntProp);
            Assert.Equal(data1.MIntProp, data0.MIntProp);
            Assert.Equal(data1.MBoolProp, data0.MBoolProp);
            Assert.Equal(data1.MUShortProp, data0.MUShortProp);
            Assert.Equal(data1.MStringProp, data0.MStringProp);
            Assert.Equal(data1.MUInt64Prop, data0.MUInt64Prop);
            Assert.Equal(data1.MInt64Prop, data0.MInt64Prop);

            // Compare
            Assert.Equal(0x0U, data0.DataSerializer.CompareMutableData(data1.DataSerializer));

            // Change fields & compare again
            data1.MByteProp = 42;
            Assert.Equal(
                0b0000_0001U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MUIntProp = uint.MaxValue - 212;
            Assert.Equal(
                0b0000_0011U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MIntProp = int.MinValue + 212;
            Assert.Equal(
                0b0000_0111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MBoolProp = true;
            Assert.Equal(
                0b0000_1111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MUShortProp = 45;
            Assert.Equal(
                0b0001_1111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MStringProp = "46";
            Assert.Equal(
                0b0011_1111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MUInt64Prop = ulong.MaxValue - 212;
            Assert.Equal(
                0b0111_1111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MInt64Prop = long.MinValue + 212;
            Assert.Equal(
                0b1111_1111U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
        }

        [Fact]
        private void CompressData()
        {
            DataWithCompressor data0 = new DataWithCompressor
            {
                CompressedInt = 0,
                CompressedFloat = 0.0f,
                CompressedInt64 = 0
            };
            DataWithCompressor data1 = new DataWithCompressor
            {
                CompressedInt = int.MinValue + 212,
                CompressedFloat = -212.0f,
                CompressedInt64 = long.MinValue + 212
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();

            // Transfer data from data1 to data0 via buffer
            uint uiFlagAll = 0xFFFF;
            RailBitBuffer buffer = new RailBitBuffer();
            data1.DataSerializer.EncodeMutableData(buffer, uiFlagAll);
            data0.DataSerializer.DecodeMutableData(buffer, uiFlagAll);

            Assert.Equal(data1.CompressedInt, data0.CompressedInt);
            Assert.Equal(data1.CompressedFloat, data0.CompressedFloat);
            Assert.Equal(data1.CompressedInt64, data0.CompressedInt64);
        }

        [Fact]
        private void CustomData()
        {
            RailSynchronizedFactory.Detect(Assembly.GetExecutingAssembly());
            DataWithCustomField data0 = new DataWithCustomField
            {
                Data = new Foo()
                {
                    A = 0,
                    B = 0
                }
            };
            DataWithCustomField data1 = new DataWithCustomField
            {
                Data = new Foo()
                {
                    A = 42,
                    B = 43
                }
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();

            // Transfer data from data1 to data0 via buffer
            uint uiFlagAll = 0xFFFF;
            RailBitBuffer buffer = new RailBitBuffer();
            data1.DataSerializer.EncodeMutableData(buffer, uiFlagAll);
            data0.DataSerializer.DecodeMutableData(buffer, uiFlagAll);

            Assert.Equal(data1.Data.A, data0.Data.A);
            Assert.Equal(data1.Data.B, data0.Data.B);
        }

        [Fact]
        private void Reset()
        {
            // Setup 2 different states
            Data data0 = new Data
            {
                MByteProp = 0,
                MUIntProp = 0,
                MIntProp = 0,
                MBoolProp = false,
                MUShortProp = 0,
                MStringProp = "",
                MUInt64Prop = 0,
                MInt64Prop = 0
            };
            Data data1 = new Data
            {
                MByteProp = 42,
                MUIntProp = uint.MaxValue - 212,
                MIntProp = int.MinValue + 212,
                MBoolProp = true,
                MUShortProp = 45,
                MStringProp = "46",
                MUInt64Prop = ulong.MaxValue - 212,
                MInt64Prop = long.MinValue + 212
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();

            // Apply state 0 to 1
            data1.DataSerializer.ApplyMutableFrom(data0.DataSerializer, 0xFFFF);
            Assert.Equal(0, data1.MByteProp);
            Assert.Equal(0U, data1.MUIntProp);
            Assert.Equal(0, data1.MIntProp);
            Assert.False(data1.MBoolProp);
            Assert.Equal(0, data1.MUShortProp);
            Assert.Equal("", data1.MStringProp);
            Assert.Equal(0UL, data1.MUInt64Prop);
            Assert.Equal(0L, data1.MInt64Prop);

            // Reset
            data1.DataSerializer.ResetAllData();
            Assert.Equal(42, data1.MByteProp);
            Assert.Equal(uint.MaxValue - 212, data1.MUIntProp);
            Assert.Equal(int.MinValue + 212, data1.MIntProp);
            Assert.True(data1.MBoolProp);
            Assert.Equal(45, data1.MUShortProp);
            Assert.Equal("46", data1.MStringProp);
            Assert.Equal(ulong.MaxValue - 212, data1.MUInt64Prop);
            Assert.Equal(long.MinValue + 212, data1.MInt64Prop);
        }

        [Theory]
        [InlineData(212)]
        [InlineData(uint.MaxValue - 212)]
        private void ReadWriteUInt(uint expected)
        {
            RailBitBuffer buffer = new RailBitBuffer();

            buffer.WriteUInt(expected);
            ulong actual = buffer.ReadUInt();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(212)]
        [InlineData(-212)]
        [InlineData(int.MaxValue - 212)]
        [InlineData(int.MinValue + 212)]
        private void ReadWriteInt(int expected)
        {
            RailBitBuffer buffer = new RailBitBuffer();

            buffer.WriteInt(expected);
            long actual = buffer.ReadInt();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(212)]
        [InlineData(ulong.MaxValue - 212)]
        private void ReadWriteUInt64(ulong expected)
        {
            RailBitBuffer buffer = new RailBitBuffer();

            buffer.WriteUInt64(expected);
            ulong actual = buffer.ReadUInt64();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(212)]
        [InlineData(-212)]
        [InlineData(long.MaxValue - 212)]
        [InlineData(long.MinValue + 212)]
        private void ReadWriteInt64(long expected)
        {
            RailBitBuffer buffer = new RailBitBuffer();

            buffer.WriteInt64(expected);
            long actual = buffer.ReadInt64();

            Assert.Equal(expected, actual);
        }
    }
    public class Foo
    {
        public int A = 0;
        public int B = 0;
    }

    public static class FooSerializer
    {
        [Encoder]
        public static void Encode(this RailBitBuffer buffer, Foo instance)
        {
            buffer.WriteInt(instance.A);
            buffer.WriteInt(instance.B);
        }
        [Decoder]
        public static Foo Decode(this RailBitBuffer buffer)
        {
            return new Foo()
            {
                A = buffer.ReadInt(),
                B = buffer.ReadInt()
            };
        }
    }
}
