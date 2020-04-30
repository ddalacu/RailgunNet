using Moq;
using RailgunNet.Factory;
using RailgunNet.Util.Pooling;
using Xunit;

namespace Tests
{
    public class RailPoolTest
    {
        public RailPoolTest()
        {
            factoryMock = new Mock<IRailFactory<A>>();
        }

        public class A : IRailPoolable<A>
        {
            IRailMemoryPool<A> IRailPoolable<A>.Pool { get; set; }

            void IRailPoolable<A>.Reset()
            {
            }
        }

        public class B : A
        {
        }

        private readonly Mock<IRailFactory<A>> factoryMock;

        [Fact]
        private void AllocateCallsFactory()
        {
            A instance = new A();
            factoryMock.Setup(f => f.Create()).Returns(instance);

            RailMemoryPool<A> pool = new RailMemoryPool<A>(factoryMock.Object);
            A allocatedObject = pool.Allocate();
            factoryMock.Verify(f => f.Create(), Times.Once);
            Assert.Same(instance, allocatedObject);
        }

        [Fact]
        private void PoolReusesInstances()
        {
            factoryMock.Setup(f => f.Create()).Returns(new A());
            RailMemoryPool<A> pool = new RailMemoryPool<A>(factoryMock.Object);
            A firstObject = pool.Allocate();
            factoryMock.Verify(f => f.Create(), Times.Once);

            pool.Deallocate(firstObject);
            A secondObject = pool.Allocate();

            factoryMock.Verify(f => f.Create(), Times.Once);
            Assert.Same(firstObject, secondObject);
        }
    }
}
