using System;

namespace RailgunNet.Util
{
    public class OnlyInAttribute : Attribute
    {
        public readonly Component Component;

        public OnlyInAttribute(Component eComponent)
        {
            Component = eComponent;
        }
    }
}