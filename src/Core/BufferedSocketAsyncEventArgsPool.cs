using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class BufferedSocketAsyncEventArgsPool : Pool<BufferedSocketAsyncEventArgs>
    {
        readonly IPool<ArraySegment<byte>> BufferPool;

        public BufferedSocketAsyncEventArgsPool( IPool<ArraySegment<byte>> bufferPool )
        {
            BufferPool = bufferPool;
        }

        public override void Release( BufferedSocketAsyncEventArgs t )
        {
            var buffer = new ArraySegment<byte>(t.Buffer, t.Offset, t.Count);

            t.SetBuffer(null, 0, 0);
            BufferPool.Release(buffer);

            base.Release(t);
        }

        protected override bool CreateObject( out BufferedSocketAsyncEventArgs obj )
        {
            obj = new BufferedSocketAsyncEventArgs();

            var buffer = BufferPool.Acquire();
            obj.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            return true;
        }
    }
}
