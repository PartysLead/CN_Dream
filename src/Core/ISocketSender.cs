using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface ISocketSender
    {
        Task SendDataAsync( Socket socket, byte[] array, int offset, int count );
    }
}
