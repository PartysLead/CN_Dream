using System;

namespace CnDream.Core
{
    public interface IDataUnpacker
    {
        (int pairId, int bytesWritten) UnpackData( ArraySegment<byte> output, ArraySegment<byte> input );
    }
}
