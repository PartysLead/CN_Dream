using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class SocketSenderPool : Pool<ISocketSender>
    {
        readonly IPool<SocketAsyncEventArgs> SendArgsPool;

        public SocketSenderPool( IPool<SocketAsyncEventArgs> sendArgsPool )
        {
            SendArgsPool = sendArgsPool;
        }

        protected override bool CreateObject( out ISocketSender obj )
        {
            obj = new SocketSender(SendArgsPool);

            return true;
        }
    }
}
