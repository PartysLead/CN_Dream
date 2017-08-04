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
        readonly IPool<SocketAsyncEventArgs> SocketAsyncEventArgsPool;
        readonly IPool<ArraySegment<byte>> BufferPool;
        readonly IDataSender DataSender;

        readonly ConcurrentDictionary<int, (Socket socket, SocketAsyncEventArgs recvArgs)> EndPointSockets;

        public EndPointStation( IChannelStation channelStation, IPool<SocketAsyncEventArgs> argsPool, IPool<ArraySegment<byte>> bufferPool, IDataSender dataSender )
        {
            ChannelStation = channelStation;
            SocketAsyncEventArgsPool = argsPool;
            BufferPool = bufferPool;
            DataSender = dataSender;

            EndPointSockets = new ConcurrentDictionary<int, (Socket, SocketAsyncEventArgs)>();
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
                // TODO: Error handling?
                await ChannelStation.SendDataAsync(pairId, args.Buffer, args.Offset, args.BytesTransferred);

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

        private (Socket, SocketAsyncEventArgs recvArgs) AcquireEndPointResources( int pairId, Socket endpointSocket )
        {
            var args = SocketAsyncEventArgsPool.Acquire();
            var buffer = BufferPool.Acquire();

            args.Completed += OnEndPointSocketReceived;
            args.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            args.UserToken = new PairInfo { PairId = pairId };

            return (endpointSocket, args);
        }

        private void ReleaseEndPointResources( (Socket, SocketAsyncEventArgs recvArgs) endpoint )
        {
            var recvArgs = endpoint.recvArgs;
            var recvBuffer = new ArraySegment<byte>(recvArgs.Buffer, recvArgs.Offset, recvArgs.Count);

            recvArgs.Completed -= OnEndPointSocketReceived;
            recvArgs.SetBuffer(null, 0, 0);
            recvArgs.UserToken = null;

            SocketAsyncEventArgsPool.Release(recvArgs);

            BufferPool.Release(recvBuffer);
        }

        public async Task SendDataAsync( int pairId, byte[] sendBuffer, int offset, int count )
        {
            if ( EndPointSockets.TryGetValue(pairId, out var endpoint) )
            {
                await DataSender.SendDataAsync(endpoint.socket, sendBuffer, offset, count);
            }
        }

        class PairInfo
        {
            public int PairId { get; set; }
        }
    }
}
