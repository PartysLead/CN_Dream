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

        const int Slot_Empty = 0;
        const int Slot_Clean = 1;
        const int Slot_Dirty = 2;
        const int ChannelSocketBucketSize = 256;
        readonly int[] ChannelSlots = new int[ChannelSocketBucketSize];
        readonly (Socket socket, BufferedSocketAsyncEventArgs recvArgs)[] Channels
            = new(Socket, BufferedSocketAsyncEventArgs)[ChannelSocketBucketSize];

        public void Initialize( IEndPointStation endpointStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
        }

        public void AddChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSlots[i], Slot_Dirty, Slot_Empty) != Slot_Empty )
                {
                    continue;
                }

                ref var channel = ref Channels[i];
                Debug.Assert(channel.socket == null, "BUG: Channel not cleaned up properly previously!");

                channel.socket = channelSocket;

                var recvArgs = ReceiveEventArgsPool.Acquire();
                channel.recvArgs = recvArgs;
                recvArgs.Completed += OnChannelSocketReceived;

                BeginReceive(channelSocket, recvArgs);

                Interlocked.Exchange(ref ChannelSlots[i], Slot_Clean);

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
            // TODO: Error handling??
            await EndPointStation.HandleChannelReceivedDataAsync(e.Buffer, e.Offset, e.BytesTransferred);
            BeginReceive(socket, e);
        }

        public void RemoveChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSlots[i], Slot_Dirty, Slot_Clean) != Slot_Clean )
                {
                    continue;
                }

                ref var channel = ref Channels[i];
                if ( channel.socket != channelSocket )
                {
                    Interlocked.Exchange(ref ChannelSlots[i], Slot_Clean);
                }
                else
                {
                    channel.socket = null;

                    var recvArgs = channel.recvArgs;
                    channel.recvArgs = null;
                    recvArgs.Completed -= OnChannelSocketReceived;

                    ReceiveEventArgsPool.Release(recvArgs);

                    Interlocked.Exchange(ref ChannelSlots[i], Slot_Empty);
                    break;
                }
            }

            throw new ArgumentException("BUG: Removing a channel that is not been added or being used!", nameof(channelSocket));
        }

        public async Task HandleEndPointDataReceivedAsync( int pairId, byte[] buffer, int offset, int count )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSlots[i], Slot_Dirty, Slot_Clean) != Slot_Clean )
                {
                    continue;
                }
                try
                {
                    throw new NotImplementedException();
                }
                finally
                {
                    Interlocked.Exchange(ref ChannelSlots[i], Slot_Clean);
                }
            }
        }
    }
}
