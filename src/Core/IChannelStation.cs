using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IChannelStation
    {
        void AddChannel( Socket channelSocket );
        void RemoveChannel( Socket channelSocket );

        IDataSender GetTransformer( int pairId );
        void ReturnTransformer( int pairId, IDataSender transformer );
    }
}
