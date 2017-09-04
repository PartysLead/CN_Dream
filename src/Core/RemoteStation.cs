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

            var channelId = AddChannel(channelSocket, dataPacker, dataUnpacker);

            throw new NotImplementedException();
        }

        protected override Task<int> CreateFreeChannelAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task HandleReceivedMessageAsync( string message )
        {
            if ( message.StartsWith("+E ") )
            {
                // EndPointStation.AddEndPoint( ... )
            }
            throw new NotImplementedException();
        }
    }
}
