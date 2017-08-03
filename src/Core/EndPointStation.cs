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

        readonly ConcurrentDictionary<int, (Socket socket, SocketAsyncEventArgs recvArgs, ArraySegment<byte> recvBuffer)> EndPointSockets;

        public EndPointStation( IChannelStation channelStation, IPool<SocketAsyncEventArgs> argsPool, IPool<ArraySegment<byte>> bufferPool )
        {
            ChannelStation = channelStation;
            SocketAsyncEventArgsPool = argsPool;
            BufferPool = bufferPool;
            EndPointSockets = new ConcurrentDictionary<int, (Socket, SocketAsyncEventArgs, ArraySegment<byte>)>();
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
                OnEndPointReceived(endpointSocket, args);
            }
        }

        private async void OnEndPointReceived( object sender, SocketAsyncEventArgs args )
        {
            var endpointSocket = (Socket)sender;
            var pairId = ((PairInfo)args.UserToken).PairId;
            if ( args.SocketError == SocketError.Success )
            {
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

        private (Socket, SocketAsyncEventArgs recvArgs, ArraySegment<byte>) AcquireEndPointResources( int pairId, Socket endpointSocket )
        {
            var args = SocketAsyncEventArgsPool.Acquire();
            var buffer = BufferPool.Acquire();

            args.Completed += OnEndPointReceived;
            args.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            args.UserToken = new PairInfo { PairId = pairId };

            return (endpointSocket, args, buffer);
        }

        private void ReleaseEndPointResources( (Socket socket, SocketAsyncEventArgs recvArgs, ArraySegment<byte> recvBuffer) endpoint )
        {
            var recvArgs = endpoint.recvArgs;

            recvArgs.Completed -= OnEndPointReceived;
            recvArgs.SetBuffer(null, 0, 0);
            recvArgs.UserToken = null;

            SocketAsyncEventArgsPool.Release(recvArgs);
            BufferPool.Release(endpoint.recvBuffer);
        }

        public Task SendDataAsync( int pairId, byte[] sendBuffer, int offset, int count )
        {
            throw new NotImplementedException();

            if ( EndPointSockets.TryGetValue(pairId, out var endpoint) )
            {
                var endpointSocket = endpoint.socket;

                var sendArgs = SocketAsyncEventArgsPool.Acquire();
                sendArgs.SetBuffer(sendBuffer, offset, count);

                if ( true )
                {

                }
            }
        }

        class PairInfo
        {
            public int PairId { get; set; }
        }
    }
}
