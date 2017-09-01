using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class RemoteStation : ChannelStation
    {
        IEndPointStation EndPointStation;
        IChannelStation ChannelStation;

        public async Task Run()
        {
            ChannelStation.MessageReceived += ChannelStation_MessageReceived;

            var listener = new TcpListener(IPAddress.Any, 1080);
            listener.Start();
            while ( true )
            {
                var channelSocket = await listener.AcceptSocketAsync();
                Negotiate(channelSocket);
            }
        }

        private void ChannelStation_MessageReceived( object sender, EventArgs e )
        {
            throw new NotImplementedException();
        }

        private void Negotiate( Socket channelSocket )
        {
            IDataPacker dataPacker = null;
            IDataUnpacker dataUnpacker = null;

            var channelId = ChannelStation.AddChannel(channelSocket, dataPacker, dataUnpacker);

            throw new NotImplementedException();
        }
    }
}
