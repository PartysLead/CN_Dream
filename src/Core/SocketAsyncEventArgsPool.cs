using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class SocketAsyncEventArgsPool : Pool<SocketAsyncEventArgs>, ISocketAsyncEventArgsPool
    {
        readonly IPool<ArraySegment<byte>> BufferPool;

        public SocketAsyncEventArgsPool( IPool<ArraySegment<byte>> bufferPool )
        {
            BufferPool = bufferPool;
        }

        public SocketAsyncEventArgs AcquireWithBuffer()
        {
            var result = Acquire();
            var buffer = BufferPool.Acquire();

            result.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            return result;
        }

        public void ReleaseWithBuffer( SocketAsyncEventArgs t )
        {
            var buffer = new ArraySegment<byte>(t.Buffer, t.Offset, t.Count);

            t.SetBuffer(null, 0, 0);

            BufferPool.Release(buffer);
            Release(t);
        }

        protected override bool CreateObject( out SocketAsyncEventArgs obj )
        {
            obj = new SocketAsyncEventArgs();

            return true;
        }
    }
}
