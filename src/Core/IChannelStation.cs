using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public interface IChannelStation
    {
        void AddChannel( Socket channelSocket );
        void RemoveChannel( Socket channelSocket );

    }
}
