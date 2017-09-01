using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class LocalStation : ChannelStation
    {
        public async Task Run()
        {
            var listener = new TcpListener(IPAddress.Any, 1080);
            listener.Start();
            var pairId = 0;
            while ( true )
            {
                var client = await listener.AcceptSocketAsync();

                _ = Task.Run(async () =>
                {
                    var remote = Socks5Negotiate(client);

                    await SendMessageAsync($"Connect {pairId} to {remote}");
                    EndPointStation.AddEndPoint(++pairId, client);
                });
            }
        }

        private IPEndPoint Socks5Negotiate( Socket client )
        {
            throw new NotImplementedException();
        }

        protected override Task<int> CreateFreeChannelAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task HandleReceivedMessageAsync( string message )
        {
            throw new NotImplementedException();
        }
    }
}
