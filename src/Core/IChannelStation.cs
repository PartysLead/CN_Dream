using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public interface IChannelStation
    {
        int AddChannel( Socket socket, IDataPacker dataPacker, IDataUnpacker dataUnpacker );
        void RemoveChannel( int channelId );

        Task HandleEndPointReceivedDataAsync( int pairId, ArraySegment<byte> buffer );

        Task SendMessageAsync( string message );
    }
}
