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
            public int i;
        }

        private class D : A
        {
            public readonly C MyC;

            public D(C arg)
            {
                MyC = arg;
            }
        }

        [Fact]
        public void CanCreateDerivedType()
        {
            RailFactory<A> factory = new RailFactory<A>(typeof(B));
            A createdObj = factory.Create();
            Assert.True(createdObj is B);
        }

        [Fact]
        public void CanCreateTypeWithParameter()
        {
            C arg = new C
            {
                i = 42
            };

            RailFactory<A> factory = new RailFactory<A>(typeof(D), new object[] {arg});
            A createdObj = factory.Create();
            Assert.True(createdObj is D);
            D createdD = createdObj as D;
            Assert.Same(arg, createdD.MyC);
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
