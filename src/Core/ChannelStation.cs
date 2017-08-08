using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class ChannelStation : IChannelStation
    {
        IEndPointStation EndPointStation;
        IPool<BufferedSocketAsyncEventArgs> ReceiveEventArgsPool;

        const int PairId_Idle = -1;
        const int PairId_Invalid = 0;
        const int ChannelStatus_NoChannel = 0;
        const int ChannelStatus_Idle = 1;
        const int ChannelStatus_Pending = 2;
        const int ChannelSocketBucketSize = 256;
        readonly int[] ChannelStatuses = new int[ChannelSocketBucketSize];
        readonly (int pairId, Socket socket, BufferedSocketAsyncEventArgs recvArgs)[] Channels
            = new(int, Socket, BufferedSocketAsyncEventArgs)[ChannelSocketBucketSize];

        public void Initialize( IEndPointStation endpointStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
        }

        public void AddChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelStatuses[i], ChannelStatus_Pending, ChannelStatus_NoChannel) != ChannelStatus_NoChannel )
                {
                    continue;
                }

                ref var channel = ref Channels[i];
                Debug.Assert(channel.socket == null, "BUG: Channel not cleaned up properly previously!");

                channel.socket = channelSocket;
                channel.pairId = PairId_Idle;

                var recvArgs = ReceiveEventArgsPool.Acquire();
                channel.recvArgs = recvArgs;
                recvArgs.Completed += OnChannelSocketReceived;

                BeginReceive(channelSocket, recvArgs);

                Interlocked.Exchange(ref ChannelStatuses[i], ChannelStatus_Idle);

                break;
            }

            throw new NotImplementedException("Cannot handle adding more channels for now..");
        }

        private void BeginReceive( Socket channelSocket, SocketAsyncEventArgs recvArgs )
        {
            if ( !channelSocket.ReceiveAsync(recvArgs) )
            {
                OnChannelSocketReceived(channelSocket, recvArgs);
            }
        }

        private async void OnChannelSocketReceived( object sender, SocketAsyncEventArgs e )
        {
            var socket = (Socket)sender;
            await EndPointStation.HandleChannelReceivedDataAsync(e.Buffer, e.Offset, e.BytesTransferred);
            BeginReceive(socket, e);
        }

        public void RemoveChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelStatuses[i], ChannelStatus_Pending, ChannelStatus_Idle) != ChannelStatus_Idle )
                {
                    continue;
                }

                ref var channel = ref Channels[i];
                if ( channel.socket != channelSocket )
                {
                    Interlocked.Exchange(ref ChannelStatuses[i], ChannelStatus_Idle);
                }
                else
                {
                    channel.pairId = PairId_Invalid;
                    channel.socket = null;

                    var recvArgs = channel.recvArgs;
                    channel.recvArgs = null;
                    recvArgs.Completed -= OnChannelSocketReceived;

                    ReceiveEventArgsPool.Release(recvArgs);

                    Interlocked.Exchange(ref ChannelStatuses[i], ChannelStatus_NoChannel);
                    break;
                }
            }

            throw new ArgumentException("BUG: Removing a channel that is not been added or being used!", nameof(channelSocket));
        }

        public async Task HandleEndPointDataReceivedAsync( int pairId, byte[] buffer, int offset, int count )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelStatuses[i], ChannelStatus_Pending, ChannelStatus_Idle) != ChannelStatus_Idle )
                {
                    continue;
                }
                try
                {
                    throw new NotImplementedException();
                }
                finally
                {
                    Interlocked.Exchange(ref ChannelStatuses[i], ChannelStatus_Idle);
                }
            }
        }
    }
}
