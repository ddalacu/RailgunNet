using RailgunNet.System.Encoding.Compressors;

namespace Tests.Example
{
    public static class Compression
    {
        public static readonly RailFloatCompressor Coordinate =
            new RailFloatCompressor(-512.0f, 512.0f, GameMath.COORDINATE_PRECISION / 10.0f);
    }
}
