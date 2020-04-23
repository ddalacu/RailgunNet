using System;

namespace Railgun
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