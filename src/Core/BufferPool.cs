using System;
using System.Collections.Generic;
using System.Text;

namespace CnDream.Core
{
    public class BufferPool : Pool<ArraySegment<byte>>
    {
        protected override ArraySegment<byte> CreateObject()
        {
            throw new NotImplementedException();
        }
    }
}
