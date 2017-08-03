using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class SocketAsyncEventArgsPool : Pool<SocketAsyncEventArgs>
    {
        protected override SocketAsyncEventArgs CreateObject()
        {
            throw new NotImplementedException();
        }
    }
}
