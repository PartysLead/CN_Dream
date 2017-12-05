using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CnDream.Core.Test
{
    public class UnpackerTests
    {
        [Fact]
        public void SmallPayload()
        {
            var unpacker = new DataUnpacker(new IdentityTransformer());
            var input = new byte[]
            {
                1, 2, 3, 2,
                1, 0, 0, 0,
                2, 0, 0, 0,
                4, 0, 0, 0,

                1, 2, 3, 4,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
            };
            var output = new byte[16];

            var unpacked = unpacker.UnpackData(
                new ArraySegment<byte>(output),
                out var bytesWritten, out var bytesRead,
                out var pairId, out var serialId, out var payloadSize, new ArraySegment<byte>(input));

            Assert.True(unpacked);

            Assert.Equal(payloadSize, bytesWritten);
            Assert.Equal(input.Length, bytesRead);

            Assert.Equal(1, pairId);
            Assert.Equal(2, serialId);
            Assert.Equal(4, payloadSize);
        }

        [Fact]
        public void SmallPayloadSliced()
        {
            var unpacker = new DataUnpacker(new IdentityTransformer());
            var input = new byte[]
            {
                1, 2, 3, 2,
                1, 0, 0, 0,
                2, 0, 0, 0,
                4, 0, 0, 0,

                1, 2, 3, 4,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
            };
            var output = new byte[16];

            var unpacked = false;
            int bytesWritten, bytesRead, pairId, serialId, payloadSize;
            for ( int i = 0; i < 4; i++ )
            {
                unpacked = unpacker.UnpackData(
                    new ArraySegment<byte>(output),
                    out bytesWritten, out bytesRead,
                    out pairId, out serialId, out payloadSize, new ArraySegment<byte>(input, i * 4, 4));

                Assert.False(unpacked);
            }

            unpacked = unpacker.UnpackData(
                new ArraySegment<byte>(output),
                out bytesWritten, out bytesRead,
                out pairId, out serialId, out payloadSize, new ArraySegment<byte>(input, 4 * 4, 16));

            Assert.True(unpacked);

            Assert.Equal(payloadSize, bytesWritten);
            Assert.Equal(16, bytesRead);

            Assert.Equal(1, pairId);
            Assert.Equal(2, serialId);
            Assert.Equal(4, payloadSize);
        }
    }
}
