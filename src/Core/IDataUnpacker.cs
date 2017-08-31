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
            out int bytesWritten, out int bytesRead,
            out int pairId, out int serialId, out int payloadSize,
            ArraySegment<byte> input
        );
    }
}
