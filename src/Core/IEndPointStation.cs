using System;
using System.Net.Sockets;

namespace CnDream.Core
{
    public interface IEndPointStation
    {
        void AddEndPoint( int pairId, Socket endpointSocket );
        void RemoveEndPoint( int pairId );

        Socket FindEndPoint( int pairId );
    }
}
