using System;
using System.Linq;
using Xunit;

namespace CnDream.Core.Test
{
    public class PackerTests
    {
        [Fact]
        public void SmallPayload()
        {
            var packer = new DataPacker(new IdentityTransformer());
            var output = new byte[32];
            var input = new byte[5];

            new Random().NextBytes(input);

            var bytesWritten = packer.PackData(1, 2, new ArraySegment<byte>(input), new ArraySegment<byte>(output));

            VerifyOutput(input, output, bytesWritten);
        }

        [Fact]
        public void LargePayload()
        {
            var packer = new DataPacker(new IdentityTransformer());
            var output = new byte[160];
            var input = new byte[128];

            new Random().NextBytes(input);

            var bytesWritten = packer.PackData(1, 2, new ArraySegment<byte>(input), new ArraySegment<byte>(output));

            VerifyOutput(input, output, bytesWritten);
        }

        private void VerifyOutput( byte[] input, byte[] output, int bytesWritten )
        {
            Assert.True(bytesWritten > input.Length);
            Assert.True(bytesWritten > 0 && bytesWritten <= output.Length);

            var pos = ParseHeader(output, bytesWritten);
            Assert.True(pos > 0 && pos < bytesWritten - 1);

            var pairId = DataUnpacker.ReadInt(output, ref pos);
            Assert.Equal(1, pairId);

            var serialId = DataUnpacker.ReadInt(output, ref pos);
            Assert.Equal(2, serialId);

            var payloadSize = DataUnpacker.ReadInt(output, ref pos);
            Assert.Equal(input.Length, payloadSize);

            Assert.True(Enumerable.SequenceEqual(output.Skip(pos).Take(input.Length), input));
        }

        private int ParseHeader( byte[] output, int bytesWritten )
        {
            for ( int i = 1; i < bytesWritten; i++ )
            {
                if ( output[i] < output[i - 1] )
                {
                    return i + 1;
                }
            }

            return -1;
        }
    }
}
