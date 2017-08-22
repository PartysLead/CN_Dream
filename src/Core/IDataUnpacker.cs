using System;

namespace CnDream.Core
{
    public interface IDataUnpacker
    {
        (int pairId, int bytes)[] UnpackData( ArraySegment<byte> output, ArraySegment<byte> input );
    }
}
