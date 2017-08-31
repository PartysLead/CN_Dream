using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class RemoteStation
    {
        IEndPointStation EndPointStation;
        IChannelStation ChannelStation;

        public async Task Run()
        {
            var listener = new TcpListener(IPAddress.Any, 1080);
            listener.Start();
            while ( true )
            {
                var channelSocket = await listener.AcceptSocketAsync();
                Negotiate(channelSocket);
            }
        }

        private void Negotiate( Socket channelSocket )
        {
            IDataPacker dataPacker = null;
            IDataUnpacker dataUnpacker = null;

            ChannelStation.TryAddChannel(channelSocket, dataPacker, dataUnpacker, out var cid);

            throw new NotImplementedException();
        }
    }
}
