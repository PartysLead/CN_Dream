using System;

namespace CnDream.Core
{
    public interface IDataUnpacker
    {
        int UnpackData( ArraySegment<byte> output, byte[] buffer, int offset, int count );
    }
}
