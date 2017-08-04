using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public interface IDataSenderProvider
    {
        IDataSender ProvideDataSender( int pairId, Socket socket );
    }
}
