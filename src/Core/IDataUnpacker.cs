using System;

namespace CnDream.Core
{
    public interface IDataUnpacker
    {
        bool UnpackData( ArraySegment<byte> output, out int bytes, out int? pairId, out int? recvId, ArraySegment<byte> input );
    }
}
