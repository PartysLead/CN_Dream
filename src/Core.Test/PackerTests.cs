using System;
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
            packer.PackData(1, 2, new ArraySegment<byte>(input), new ArraySegment<byte>(output));
        }
    }
}
