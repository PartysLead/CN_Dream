using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public class BufferPool : Pool<ArraySegment<byte>>
    {
        byte[] TheBigBuffer;

        protected override bool CreateObject( out ArraySegment<byte> obj )
        {
            throw new NotImplementedException();
        }
    }
}
