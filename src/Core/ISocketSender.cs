using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface ISocketSender
    {
        void SetSocket( Socket socket );
        void SetBuffer( ArraySegment<byte> buffer );

        Task SendDataAsync();
    }
}
