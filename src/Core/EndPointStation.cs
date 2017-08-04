using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class EndPointStation : IEndPointStation
    {
        readonly IChannelStation ChannelStation;
        readonly IPool<BufferedSocketAsyncEventArgs> ReceiveEventArgsPool;
        readonly IDataSenderProvider DetransformerProvider;

        readonly ConcurrentDictionary<int, (Socket socket, BufferedSocketAsyncEventArgs recvArgs)> EndPointSockets;

        public EndPointStation( IChannelStation channelStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool, IDataSenderProvider detransformerProvider )
        {
            ChannelStation = channelStation;
            ReceiveEventArgsPool = recvArgsPool;
            DetransformerProvider = detransformerProvider;

            EndPointSockets = new ConcurrentDictionary<int, (Socket, BufferedSocketAsyncEventArgs)>();
        }

        public void AddEndPoint( int pairId, Socket endpointSocket )
        {
            var endpoint = AcquireEndPointResources(pairId, endpointSocket);

            if ( EndPointSockets.TryAdd(pairId, endpoint) )
            {
                BeginReceive(endpointSocket, endpoint.recvArgs);
            }
            else
            {
                ReleaseEndPointResources(endpoint);
            }
        }

        private void BeginReceive( Socket endpointSocket, SocketAsyncEventArgs args )
        {
            if ( !endpointSocket.ReceiveAsync(args) )
            {
                OnEndPointSocketReceived(endpointSocket, args);
            }
        }

        private async void OnEndPointSocketReceived( object sender, SocketAsyncEventArgs args )
        {
            var endpointSocket = (Socket)sender;
            var pairId = ((PairInfo)args.UserToken).PairId;
            if ( args.SocketError == SocketError.Success )
            {
                // TODO: Error handling??
                await ChannelStation.GetTransformer(pairId).SendDataAsync(args.Buffer, args.Offset, args.BytesTransferred);

                BeginReceive(endpointSocket, args);
            }
            else
            {
                RemoveEndPoint(pairId);
            }
        }

        public void RemoveEndPoint( int pairId )
        {
            if ( EndPointSockets.TryRemove(pairId, out var endpoint) )
            {
                ReleaseEndPointResources(endpoint);
            }
        }

        private (Socket, BufferedSocketAsyncEventArgs recvArgs) AcquireEndPointResources( int pairId, Socket endpointSocket )
        {
            var args = ReceiveEventArgsPool.Acquire();

            args.Completed += OnEndPointSocketReceived;
            args.UserToken = new PairInfo { PairId = pairId };

            return (endpointSocket, args);
        }

        private void ReleaseEndPointResources( (Socket, BufferedSocketAsyncEventArgs recvArgs) endpoint )
        {
            var recvArgs = endpoint.recvArgs;
            var recvBuffer = new ArraySegment<byte>(recvArgs.Buffer, recvArgs.Offset, recvArgs.Count);

            recvArgs.Completed -= OnEndPointSocketReceived;
            recvArgs.UserToken = null;

            ReceiveEventArgsPool.Release(recvArgs);
        }

        public IDataSender GetDetransformer( int pairId )
        {
            if ( EndPointSockets.TryGetValue(pairId, out var endpoint) )
            {
                return DetransformerProvider.ProvideDataSender(pairId, endpoint.socket);
            }
            else
            {
                // TODO: Error handling??
                return null;
            }
        }

        class PairInfo
        {
            public int PairId { get; set; }
        }
    }
}
