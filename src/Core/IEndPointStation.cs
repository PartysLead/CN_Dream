using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IEndPointStation
    {
        void AddEndPoint( int pairId, Socket endpointSocket );
        void RemoveEndPoint( int pairId );

        Task HandleChannelReceivedDataAsync( byte[] buffer, int offset, int count );
    }
}
