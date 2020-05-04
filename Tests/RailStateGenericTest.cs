using RailgunNet.Logic;
using RailgunNet.Logic.State;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.Util.Pooling;
using Tests.Example;
using Xunit;

namespace Tests
{
    public class RailStateGenericTest
    {
        private class Data : RailState
        {
            [Mutable] public int MByteProp { get; set; }
            [Mutable] public uint MUIntProp { get; set; }
            [Mutable] public int MIntProp { get; set; }
            [Mutable] public bool MBoolProp { get; set; }
            [Mutable] public ushort MUShortProp { get; set; }
            [Mutable] public string MStringProp { get; set; }
        }

        private class DataWithCompressor : RailState
        {
            [Mutable] [Compressor(typeof(MyIntCompressor))] public int CompressedInt { get; set; }

            [Mutable]
            [Compressor(typeof(MyFloatCompressor))]
            public float CompressedFloat { get; set; }
        }

        private class MyIntCompressor : RailIntCompressor
        {
            public MyIntCompressor() : base(0, 100)
            {
            }

            [Encoder(Encoders.SupportedType.Int_t)]
            public void Write(RailBitBuffer buffer, int i)
            {
                buffer.WriteInt(this, i);
            }

            [Decoder(Encoders.SupportedType.Int_t)]
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

            [Encoder(Encoders.SupportedType.Float_t)]
            public void Write(RailBitBuffer buffer, float f)
            {
                buffer.WriteFloat(this, f);
            }

            [Decoder(Encoders.SupportedType.Float_t)]
            public float Read(RailBitBuffer buffer)
            {
                return buffer.ReadFloat(this);
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
                MStringProp = ""
            };
            Data data1 = new Data
            {
                MByteProp = 42,
                MUIntProp = 43,
                MIntProp = 44,
                MBoolProp = true,
                MUShortProp = 45,
                MStringProp = "46"
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();
            Assert.NotEqual(data1.MByteProp, data0.MByteProp);
            Assert.NotEqual(data1.MUIntProp, data0.MUIntProp);
            Assert.NotEqual(data1.MIntProp, data0.MIntProp);
            Assert.NotEqual(data1.MBoolProp, data0.MBoolProp);
            Assert.NotEqual(data1.MUShortProp, data0.MUShortProp);
            Assert.NotEqual(data1.MStringProp, data0.MStringProp);

            // Apply mutable data
            data0.DataSerializer.ApplyMutableFrom(data1.DataSerializer, 0xFFFF);

            // And now they should be equal
            Assert.Equal(data1.MByteProp, data0.MByteProp);
            Assert.Equal(data1.MUIntProp, data0.MUIntProp);
            Assert.Equal(data1.MIntProp, data0.MIntProp);
            Assert.Equal(data1.MBoolProp, data0.MBoolProp);
            Assert.Equal(data1.MUShortProp, data0.MUShortProp);
            Assert.Equal(data1.MStringProp, data0.MStringProp);
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
                MStringProp = ""
            };
            Data data1 = new Data
            {
                MByteProp = 0,
                MUIntProp = 0,
                MIntProp = 0,
                MBoolProp = false,
                MUShortProp = 0,
                MStringProp = ""
            };
            ((IRailPoolable<RailState>) data0).Allocated();
            ((IRailPoolable<RailState>) data1).Allocated();
            Assert.Equal(data1.MByteProp, data0.MByteProp);
            Assert.Equal(data1.MUIntProp, data0.MUIntProp);
            Assert.Equal(data1.MIntProp, data0.MIntProp);
            Assert.Equal(data1.MBoolProp, data0.MBoolProp);
            Assert.Equal(data1.MUShortProp, data0.MUShortProp);
            Assert.Equal(data1.MStringProp, data0.MStringProp);

            // Compare
            Assert.Equal(0x0U, data0.DataSerializer.CompareMutableData(data1.DataSerializer));

            // Change fields & compare again
            data1.MByteProp = 42;
            Assert.Equal(
                0b0000_0001U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MUIntProp = 43;
            Assert.Equal(
                0b0000_0011U,
                data0.DataSerializer.CompareMutableData(data1.DataSerializer));
            data1.MIntProp = 44;
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
        }

        [Fact]
        private void CompressInt()
        {
            DataWithCompressor data0 = new DataWithCompressor
            {
                CompressedInt = 0,
                CompressedFloat = 0.0f
            };
            DataWithCompressor data1 = new DataWithCompressor
            {
                CompressedInt = 42,
                CompressedFloat = 43.0f
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
                MStringProp = ""
            };
            Data data1 = new Data
            {
                MByteProp = 42,
                MUIntProp = 43,
                MIntProp = 44,
                MBoolProp = true,
                MUShortProp = 45,
                MStringProp = "46"
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

            // Reset
            data1.DataSerializer.ResetAllData();
            Assert.Equal(42, data1.MByteProp);
            Assert.Equal(43U, data1.MUIntProp);
            Assert.Equal(44, data1.MIntProp);
            Assert.True(data1.MBoolProp);
            Assert.Equal(45, data1.MUShortProp);
            Assert.Equal("46", data1.MStringProp);
        }
    }
}
