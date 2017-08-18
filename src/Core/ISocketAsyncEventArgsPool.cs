using System;
using System.Net.Sockets;

namespace CnDream.Core
{
    public interface ISocketAsyncEventArgsPool : IPool<SocketAsyncEventArgs>
    {
        SocketAsyncEventArgs AcquireWithBuffer();
        void ReleaseWithBuffer( SocketAsyncEventArgs t );
    }
}
