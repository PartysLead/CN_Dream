using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IDataSender
    {
        Task SendDataAsync( Socket socket, byte[] buffer, int offset, int count );
    }
}
