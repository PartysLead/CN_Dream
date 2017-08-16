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
        bool TryAddChannel( Socket channelSocket, out int channelId );
        bool TryRemoveChannel( int channelId, out Socket channelSocket );

        Task HandleEndPointReceivedDataAsync( int pairId, byte[] buffer, int offset, int count );
    }
}
