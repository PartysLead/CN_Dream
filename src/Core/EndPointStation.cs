using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CnDream.Core
{
    public class EndPointStation : IEndPointStation
    {
        readonly IChannelStation ChannelStation;
        readonly IPool<SocketAsyncEventArgs> SocketAsyncEventArgsPool;
        readonly IPool<ArraySegment<byte>> BufferPool;
        readonly Dictionary<int, (Socket socket, SocketAsyncEventArgs socketAsyncEventArgs, ArraySegment<byte> buffer)> EndPointSockets;

        public EndPointStation( IChannelStation channelStation, IPool<SocketAsyncEventArgs> saePool, IPool<ArraySegment<byte>> bufferPool )
        {
            ChannelStation = channelStation;
            SocketAsyncEventArgsPool = saePool;
            BufferPool = bufferPool;
            EndPointSockets = new Dictionary<int, (Socket, SocketAsyncEventArgs, ArraySegment<byte>)>();
        }

        public void AddEndPoint( int pairId, Socket endpointSocket )
        {
            var socketAsyncEventArgs = SocketAsyncEventArgsPool.Acquire();
            var buffer = BufferPool.Acquire();
            socketAsyncEventArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            socketAsyncEventArgs.UserToken = new PairInfo { PairId = pairId };

            EndPointSockets.Add(pairId, (endpointSocket, socketAsyncEventArgs, buffer));

            if ( endpointSocket.ReceiveAsync(socketAsyncEventArgs) )
            {
                socketAsyncEventArgs.Completed += OnEndPointReceived;
            }
            else
            {
                OnEndPointReceived(endpointSocket, socketAsyncEventArgs);
            }
        }

        private async void OnEndPointReceived( object sender, SocketAsyncEventArgs socketAsyncEventArgs )
        {
            var endpointSocket = (Socket)sender;
            var pairId = ((PairInfo)socketAsyncEventArgs.UserToken).PairId;
            if ( socketAsyncEventArgs.SocketError == SocketError.Success )
            {
                var channel = ChannelStation.GetStream(pairId);
                await channel.WriteAsync(socketAsyncEventArgs.Buffer, socketAsyncEventArgs.Offset, socketAsyncEventArgs.BytesTransferred);

                if ( !endpointSocket.ReceiveAsync(socketAsyncEventArgs) )
                {
                    OnEndPointReceived(endpointSocket, socketAsyncEventArgs);
                }
            }
            else
            {
                RemoveEndPoint(pairId);
            }
        }

        public void RemoveEndPoint( int pairId )
        {
            throw new NotImplementedException();
            if ( EndPointSockets.TryGetValue(pairId, out var tuple) )
            {
            }
        }

        public bool TryFindEndPoint( int pairId, out Stream sendStream )
        {
            if ( EndPointSockets.TryGetValue(pairId, out var tuple))
            {
                sendStream = new NetworkStream(tuple.socket);
                return true;
            }

            sendStream = null;
            return false;
        }

        class PairInfo
        {
            public int PairId { get; set; }
        }
    }
}
