﻿using System;
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

        Task HandleEndPointDataReceivedAsync( int pairId, byte[] buffer, int offset, int count );
    }
}
