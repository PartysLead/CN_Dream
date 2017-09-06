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
                var salt = new byte[16]; // TODO: config
                var iv = new byte[16];

                FillBuffer(ns, salt);
                FillBuffer(ns, iv);

                var d = new Rfc2898DeriveBytes("password", salt, iterations: 10000); // TODO:!!!!
                var aes = Aes.Create();
                aes.Key = d.GetBytes(16);
                aes.IV = iv;

                var dataPacker = new DataPacker(aes.CreateEncryptor(), default(ArraySegment<byte>)); // TODO:????
                var dataUnpacker = new DataUnpacker(aes.CreateDecryptor()); // TODO:????

                var channelId = AddChannel(channelSocket, dataPacker, dataUnpacker);
            }
        }

        void FillBuffer( NetworkStream ns, byte[] buffer )
        {
            var bytesRead = 0;
            var bytesWritten = 0;
            do
            {
                bytesRead = ns.Read(buffer, bytesRead, buffer.Length);
                bytesWritten += bytesRead;
            }
            while ( bytesWritten < 16 );
        }

        protected override Task<int> CreateFreeChannelAsync()
        {
            throw new NotImplementedException();
        }

        protected override async Task HandleReceivedMessageAsync( string message )
        {
            if ( message.StartsWith("+E ") )
            {
                var parts = message.Split(' ');
                var pairId = Int32.Parse(parts[1]);
                var act = Byte.Parse(parts[2]);
                var addr = parts[3];
                var sep = addr.LastIndexOf(':');
                var host = addr.Substring(0, sep);
                var port = Int32.Parse(addr.Substring(sep + 1));

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                switch ( act )
                {
                    case 1: // Connect
                        await socket.ConnectAsync(host, port);
                        break;
                    case 2: // Bind
                    case 3: // Udp Associate
                        throw new NotImplementedException();
                    default:
                        break;
                }

                EndPointStation.AddEndPoint(pairId, socket);
            }
        }
    }
}
