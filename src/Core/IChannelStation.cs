﻿using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IChannelStation
    {
        bool TryAddChannel( Socket channelSocket, out int channelId );
        bool TryRemoveChannel( int channelId, out Socket channelSocket );

        Task HandleEndPointReceivedDataAsync( int pairId, ArraySegment<byte> buffer );
    }
}
