using System;

namespace RailgunNet.Util
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Method)]
    public class OnlyInAttribute : Attribute
    {
        private Component Component { get; }

        public OnlyInAttribute(Component eComponent)
        {
            Component = eComponent;
        }
    }
}