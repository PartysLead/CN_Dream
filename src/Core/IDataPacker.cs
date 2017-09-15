using System;

namespace CnDream.Core
{
    public interface IDataPacker
    {
        /// <returns>bytes written</returns>
        int PackData( int pairId, int serialId, ArraySegment<byte> input, ArraySegment<byte> output );
    }
}
