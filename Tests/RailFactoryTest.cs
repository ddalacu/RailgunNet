using System;
using RailgunNet.Factory;
using Xunit;

namespace Tests
{
    public class RailFactoryTest
    {
        private interface IA
        {
        }
        private abstract class A : IA
        {
        }

        private class B : A
        {
        }
        private class C
        {

        }
        [Fact]
        public void CanCreateDerivedType()
        {
            var factory = new RailFactory<A>(typeof(B));
            var createdObj = factory.Create();
            Assert.True(createdObj is B);
        }
        [Fact]
        public void ThrowsWhenTypeIsAbstract()
        {
            Assert.Throws<ArgumentException>(() => new RailFactory<IA>());
            Assert.Throws<ArgumentException>(() => new RailFactory<IA>(typeof(A)));
        }
        [Fact]
        public void ThrowsWhenTypeIsNotDerived()
        {
            Assert.Throws<ArgumentException>(() => new RailFactory<IA>(typeof(C)));
        }
    }
}
