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

            Assert.Equal(true, unpacked);

            Assert.Equal(payloadSize, bytesWritten);
            Assert.Equal(input.Length, bytesRead);

            Assert.Equal(1, pairId);
            Assert.Equal(2, serialId);
            Assert.Equal(4, payloadSize);
        }
    }
}
