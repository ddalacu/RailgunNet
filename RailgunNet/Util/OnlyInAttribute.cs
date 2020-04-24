using System;

namespace RailgunNet.Util
{
    [AttributeUsage(
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Class |
        AttributeTargets.Method |
        AttributeTargets.Parameter)]
    public class OnlyInAttribute : Attribute
    {
        public OnlyInAttribute(Component eComponent)
        {
            Component = eComponent;
        }

        private Component Component { get; }
    }
}
