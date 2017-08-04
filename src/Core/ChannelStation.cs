using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class ChannelStation : IChannelStation
    {
        readonly IEndPointStation EndPointStation;
        readonly IPool<BufferedSocketAsyncEventArgs> ReceiveEventArgsPool;
        readonly IDataSenderProvider TransformerProvider;

        ConcurrentQueue<Socket> IdleSockets;

        public ChannelStation( IEndPointStation endpointStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool, IDataSenderProvider transformerProvider )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            TransformerProvider = transformerProvider;

            IdleSockets = new ConcurrentQueue<Socket>();
        }

        public void AddChannel( Socket channelSocket )
        {
            IdleSockets.Enqueue(channelSocket);
        }

        public void RemoveChannel( Socket channelSocket )
        {
            
        }

        public IDataSender GetTransformer( int pairId )
        {
            if ( IdleSockets.TryDequeue(out var socket) )
            {
                return TransformerProvider.ProvideDataSender(pairId, socket);
            }
            else
            {
                // TODO: ????
                return null;
            }
        }
    }
}
