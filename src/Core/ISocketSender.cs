using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface ISocketSender
    {
        Task SendDataAsync( Socket socket, ArraySegment<byte> buffer );
    }
}
