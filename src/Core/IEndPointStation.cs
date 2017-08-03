using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public interface IEndPointStation
    {
        void AddEndPoint( int pairId, Socket endpointSocket );
        void RemoveEndPoint( int pairId );
        bool TryFindEndPoint( int pairId, out Stream sendStream );
    }
}
