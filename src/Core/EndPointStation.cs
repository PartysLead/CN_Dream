using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class EndPointStation : IEndPointStation
    {
        IChannelStation ChannelStation;
        ISocketAsyncEventArgsPool ReceiveEventArgsPool;

        readonly ConcurrentDictionary<int, (Socket socket, SocketAsyncEventArgs recvArgs)> EndPointSockets
            = new ConcurrentDictionary<int, (Socket, SocketAsyncEventArgs)>();

        public void Initialize( IChannelStation channelStation, ISocketAsyncEventArgsPool recvArgsPool )
        {
            ChannelStation = channelStation;
            ReceiveEventArgsPool = recvArgsPool;
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
                // TODO: ?????
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

        private async void OnEndPointSocketReceived( object sender, SocketAsyncEventArgs e )
        {
            var endpointSocket = (Socket)sender;
            var pairId = ((PairInfo)e.UserToken).PairId;
            if ( e.SocketError == SocketError.Success )
            {
                if ( e.BytesTransferred > 0 )
                {
                    // TODO: Error handling??
                    await ChannelStation.HandleEndPointReceivedDataAsync(pairId, new ArraySegment<byte>(e.Buffer, e.Offset, e.BytesTransferred));

                    BeginReceive(endpointSocket, e);
                }
            }
            else
            {
                RemoveEndPoint(pairId);
            }
        }

        private (Socket, SocketAsyncEventArgs recvArgs) AcquireEndPointResources( int pairId, Socket endpointSocket )
        {
            var recvArgs = ReceiveEventArgsPool.AcquireWithBuffer();

            recvArgs.Completed += OnEndPointSocketReceived;
            recvArgs.UserToken = new PairInfo { PairId = pairId };

            return (endpointSocket, recvArgs);
        }

        private void ReleaseEndPointResources( (Socket, SocketAsyncEventArgs recvArgs) endpoint )
        {
            var recvArgs = endpoint.recvArgs;

            recvArgs.Completed -= OnEndPointSocketReceived;
            recvArgs.UserToken = null;

            ReceiveEventArgsPool.ReleaseWithBuffer(recvArgs);
        }

        public void RemoveEndPoint( int pairId )
        {
            if ( EndPointSockets.TryRemove(pairId, out var endpoint) )
            {
                ReleaseEndPointResources(endpoint);
            }
        }

        public Socket FindEndPoint( int pairId )
        {
            if ( EndPointSockets.TryGetValue(pairId, out var endpoint) )
            {
                return endpoint.socket;
            }
            else
            {
                return null;
            }
        }

        class PairInfo
        {
            public int PairId { get; set; }
        }
    }
}
