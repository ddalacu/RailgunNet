using System;

namespace RailgunNet.Logic.State
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class MutableAttribute : Attribute
    {
        
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ImmutableAttribute : Attribute
    {
        
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ControllerAttribute : Attribute
    {
        
    }
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CompressorAttribute : Attribute
    {
        public Type Compressor { get; }
        public CompressorAttribute(Type compressor)
        {
            Compressor = compressor;
        }
    }
}
