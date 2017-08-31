using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public class DataUnpacker : IDataUnpacker
    {
        public bool UnpackData
        (
            ArraySegment<byte> output,
            out (int bytesWritten, int bytesRead, int? pairId, int? serialId, int? payloadSize) description,
            ArraySegment<byte> input
        )
        {
            throw new NotImplementedException();
        }
    }
}
