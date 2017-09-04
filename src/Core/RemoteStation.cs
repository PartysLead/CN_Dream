using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
            using ( var ns = new NetworkStream(channelSocket, ownsSocket: false) )
            {
                var iv = new byte[16];
                ns.Read(iv, 0, iv.Length);

                var d = new Rfc2898DeriveBytes("password", GenerateSalt()); // TODO:!!!!

                var aes = Aes.Create();
                aes.IV = iv;
                aes.Key = d.GetBytes(16);

                var dataPacker = new DataPacker(aes.CreateEncryptor(), default(ArraySegment<byte>)); // TODO:????
                var dataUnpacker = new DataUnpacker(aes.CreateDecryptor()); // TODO:????

                var channelId = AddChannel(channelSocket, dataPacker, dataUnpacker);
            }
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
