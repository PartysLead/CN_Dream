using System;

namespace CnDream.Core
{
    public interface IDataPacker
    {
        int PackData( ArraySegment<byte> output, int pairId, bool wasPaired, ArraySegment<byte> input );
    }
}
