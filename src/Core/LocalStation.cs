using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class LocalStation : ChannelStation
    {
        Settings Settings;

        public async Task Run( Settings settings )
        {
            Settings = settings;

            var local = settings.Local;
            var listener = new TcpListener(local.Listen, local.Port);

            listener.Start();

            var pairIdSeed = 0;
            while ( true )
            {
                var client = await listener.AcceptSocketAsync();

                var pairId = Interlocked.Increment(ref pairIdSeed);
                _ = Task.Run(async () =>
                {
                    var command = Negotiate(client);
                    if ( command != null )
                    {
                        var cmd = command.Value;
                        await SendMessageAsync($"+E {pairId} {cmd.act} {cmd.addr}");
                        EndPointStation.AddEndPoint(pairId, client);
                    }
                });
            }
        }

        private (int act, string addr)? Negotiate( Socket client )
        {
            (int act, string addr) result;
            var ns = new NetworkStream(client, ownsSocket: false);
            var @byte = 0;

            // Client sends us their supported authentication methods
            /*-----+----------+---------+
             | VER | NMETHODS | METHODS |
             +-----+----------+---------+
             |  1  |     1    | 1 ~ 255 |
             +-----+----------+---------*/

            @byte = ns.ReadByte();
            if ( @byte != 5 )
            {
                return null;
            }
            @byte = ns.ReadByte();
            for ( int i = 0; i < @byte; i++ )
            {
                ns.ReadByte();
            }

            // We reply no authentication needed
            /*----+--------+
             |VER | METHOD |
             +----+--------+
             | 1  |   1    |
             +----+--------*/

            ns.WriteByte(5);
            ns.WriteByte(0); // NO AUTHENTICATION REQUIRED

            // Client sends their intention
            /*----+-----+-------+------+----------+----------+
             |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
             +----+-----+-------+------+----------+----------+
             | 1  |  1  | X'00' |  1   | Variable |    2     |
             +----+-----+-------+------+----------+----------*/

            ns.ReadByte(); // 5
            result.act = ns.ReadByte();
            ns.ReadByte();

            int addrLen = 0;
            int dstAddrType = ns.ReadByte();
            switch ( dstAddrType )
            {
                case 1:
                    // ipv4
                    addrLen = 4;
                    break;
                case 4:
                    // ipv6
                    addrLen = 16;
                    break;
                case 3:
                    // string
                    addrLen = ns.ReadByte();
                    break;
                default:
                    break;
            }

            var dstAddr = new byte[addrLen];
            ns.Read(dstAddr, 0, dstAddr.Length);
            switch ( dstAddrType )
            {
                case 1:
                case 4:
                    result.addr = new IPAddress(dstAddr).ToString();
                    break;
                case 3:
                    result.addr = Encoding.UTF8.GetString(dstAddr);
                    break;
                default:
                    result.addr = "";
                    break;
            }

            var dstPort = 0;
            dstPort |= ns.ReadByte() << 8;
            dstPort |= ns.ReadByte();
            result.addr += $":{dstPort}";

            // We just reply everything is ok?
            /*-----+-----+-------+------+----------+----------+
             | VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
             +-----+-----+-------+------+----------+----------+
             |  1  |  1  | X'00' |  1   | Variable |    2     |
             +-----+-----+-------+------+----------+----------*/

            ns.WriteByte(5);
            ns.WriteByte(0); // succeeded
            ns.WriteByte(0);
            ns.WriteByte(1); // ipv4

            // just junk addr
            var rand = new Random();
            var bndAddr = new byte[6];
            rand.NextBytes(bndAddr);
            ns.Write(bndAddr, 0, bndAddr.Length);

            ns.Dispose();

            return result;
        }

        protected override async Task<int> CreateFreeChannelAsync()
        {
            var config = Settings.Local;

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var aes = Aes.Create();

            var d = new Rfc2898DeriveBytes(Settings.Password, Settings.SaltSize, Settings.Iterations);
            aes.Key = d.GetBytes(Settings.KeySize);

            var dataPacker = new DataPacker(aes.CreateEncryptor(), default(ArraySegment<byte>));// TODO:???
            var dataUnpacker = new DataUnpacker(aes.CreateDecryptor());// TODO:???

            await socket.ConnectAsync(config.PeerAddress, config.PeerPort);

            using ( var ns = new NetworkStream(socket, ownsSocket: false) )
            {
                await ns.WriteAsync(d.Salt, 0, d.Salt.Length);
                await ns.WriteAsync(aes.IV, 0, aes.IV.Length);
            }

            return AddChannel(socket, dataPacker, dataUnpacker);
        }

        protected override Task HandleReceivedMessageAsync( string message )
        {
            throw new NotImplementedException();
        }
    }
}
