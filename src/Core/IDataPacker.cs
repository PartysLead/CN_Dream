using System;

namespace CnDream.Core
{
    public interface IDataPacker
    {
        /// <param name="bytes">Number of bytes written to output when returns true, number of bytes read from input when returns false.</param>
        /// <returns>true when output buffer is sufficient, otherwise false.</returns>
        bool PackData( ArraySegment<byte> output, out int bytes, int pairId, int? serialId, ArraySegment<byte> input );
    }
}
