using System;

namespace CnDream.Core
{
    public interface IDataPacker
    {
        /// <returns>true when output buffer is sufficient, otherwise false.</returns>
        bool PackData( ArraySegment<byte> output, out int bytesWritten, out int bytesRead, int pairId, int? serialId, ArraySegment<byte> input );
    }
}
