using System;

namespace CnDream.Core
{
    public interface IDataUnpacker
    {
        /// <remarks>
        /// When this returns false, bytesWritten = 0, bytesRead = input.Count
        /// </remarks>
        bool UnpackData
        (
            ArraySegment<byte> output,
            out (int bytesWritten, int bytesRead, int? pairId, int? serialId, int? payloadSize) description,
            ArraySegment<byte> input
        );
    }
}
