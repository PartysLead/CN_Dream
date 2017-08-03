using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class SocketAsyncEventArgsPool : Pool<SocketAsyncEventArgs>
    {
        protected override bool CreateObject( out SocketAsyncEventArgs obj )
        {
            throw new NotImplementedException();
        }
    }
}
