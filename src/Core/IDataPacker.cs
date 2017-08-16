using System;

namespace CnDream.Core
{
    public interface IDataPacker
    {
        void PackData( ArraySegment<byte> output, int pairId, bool wasPaired, byte[] buffer, int offset, int count );
    }
}
