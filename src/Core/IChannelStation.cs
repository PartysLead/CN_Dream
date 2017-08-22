using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IChannelStation
    {
        bool TryAddChannel( Socket socket, IDataPacker dataPacker, IDataUnpacker dataUnpacker, out int channelId );
        bool TryRemoveChannel( int channelId, out Socket socket, out IDataPacker dataPacker, out IDataUnpacker dataUnpacker);

        Task HandleEndPointReceivedDataAsync( int pairId, ArraySegment<byte> buffer );
    }
}
