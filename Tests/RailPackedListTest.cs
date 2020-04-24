﻿using System.Linq;
using Moq;
using RailgunNet;
using RailgunNet.Connection;
using RailgunNet.System.Encoding;
using RailgunNet.Util.Pooling;
using Xunit;

namespace Tests
{
    public class RailPackedListTest
    {
        private int elementsCreated = 0;
        private int elementsDestroyed = 0;
        public class Foo : IRailPoolable<Foo>
        {
            public int Data { get; private set; }
            public Foo(int iData)
            {
                Data = iData;
            }
            IRailMemoryPool<Foo> IRailPoolable<Foo>.Pool { get; set; }
            void IRailPoolable<Foo>.Reset() { }
            public void ReadData(RailBitBuffer buffer)
            {
                Data = buffer.ReadInt();
            }
            public void WriteData(RailBitBuffer buffer)
            {
                buffer.WriteInt(Data);
            }
        }

        private readonly Mock<IRailMemoryPool<Foo>> poolMock;
        public RailPackedListTest()
        {
            poolMock = new Mock<IRailMemoryPool<Foo>>();
            poolMock.Setup(p => p.Allocate()).Returns(() =>
            {
                Foo foo = new Foo(elementsCreated++);
                (foo as IRailPoolable<Foo>).Pool = poolMock.Object;
                return foo;
            });
            poolMock.Setup(p => p.Deallocate(It.IsAny<Foo>()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(255)]
        void CanEncode(int iNumberOfEntries)
        {
            // Add entries to pending
            RailPackedListOutgoing<Foo> list = new RailPackedListOutgoing<Foo>();
            for (int i = 0; i < iNumberOfEntries; ++i)
            {
                list.AddPending(poolMock.Object.Allocate());
            }
            Assert.Equal(iNumberOfEntries, elementsCreated);
            poolMock.Verify(p=>p.Allocate(), Times.Exactly(iNumberOfEntries));
            poolMock.Verify(p=>p.Deallocate(It.IsAny<Foo>()), Times.Never);

            // Encode
            RailBitBuffer buffer = new RailBitBuffer();
            list.Encode(
                buffer, 
                RailConfig.PACKCAP_COMMANDS, 
                RailConfig.MAXSIZE_COMMANDUPDATE,
                foo => foo.WriteData(buffer));
            Assert.False(buffer.Empty);
            poolMock.Verify(p => p.Allocate(), Times.Exactly(iNumberOfEntries));
            poolMock.Verify(p => p.Deallocate(It.IsAny<Foo>()), Times.Never);
            Assert.Equal(iNumberOfEntries, list.Sent.Count());

            // Clear the list. This should deallocate all sent objects.
            list.Clear();
            poolMock.Verify(p => p.Allocate(), Times.Exactly(iNumberOfEntries));
            poolMock.Verify(p => p.Deallocate(It.IsAny<Foo>()), Times.Exactly(iNumberOfEntries));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(255)]
        void CanDecode(int iNumberOfEntries)
        {
            // Add entries to pending
            RailPackedListOutgoing<Foo> list = new RailPackedListOutgoing<Foo>();
            for (int i = 0; i < iNumberOfEntries; ++i)
            {
                list.AddPending(poolMock.Object.Allocate());
            }
            Assert.Equal(iNumberOfEntries, elementsCreated);
            poolMock.Verify(p => p.Allocate(), Times.Exactly(iNumberOfEntries));
            poolMock.Verify(p => p.Deallocate(It.IsAny<Foo>()), Times.Never);

            // Encode
            RailBitBuffer buffer = new RailBitBuffer();
            list.Encode(
                buffer,
                RailConfig.PACKCAP_COMMANDS,
                RailConfig.MAXSIZE_COMMANDUPDATE,
                foo => foo.WriteData(buffer));
            Assert.False(buffer.Empty);
            poolMock.Verify(p => p.Allocate(), Times.Exactly(iNumberOfEntries));
            poolMock.Verify(p => p.Deallocate(It.IsAny<Foo>()), Times.Never);
            Assert.Equal(iNumberOfEntries, list.Sent.Count());

            RailPackedListIncoming<Foo> incoming = new RailPackedListIncoming<Foo>();
            incoming.Decode(buffer, () =>
            {
                Foo foo = poolMock.Object.Allocate();
                foo.ReadData(buffer);
                return foo;
            });

            Assert.Equal(iNumberOfEntries, incoming.Received.Count());
        }
    }
}