using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class ChannelStation : IChannelStation
    {
        IEndPointStation EndPointStation;
        ISocketAsyncEventArgsPool ReceiveEventArgsPool;
        IPool<ArraySegment<byte>> DataBufferPool;
        IPool<ISocketSender> SocketSenderPool;

        int ChannelIdSeed = 0;
        ConcurrentDictionary<int, (Socket socket, SocketAsyncEventArgs recvArgs, IDataPacker dataPacker)> Channels
            = new ConcurrentDictionary<int, (Socket, SocketAsyncEventArgs, IDataPacker)>();
        ConcurrentDictionary<int, EndpointActivityInfo> EndpointActivities = new ConcurrentDictionary<int, EndpointActivityInfo>();
        ConcurrentBag<int> FreeChannels = new ConcurrentBag<int>();

        public void Initialize( IEndPointStation endpointStation, ISocketAsyncEventArgsPool recvArgsPool, IPool<ArraySegment<byte>> dataBufferPool, IPool<ISocketSender> socketSenderPool )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            DataBufferPool = dataBufferPool;
            SocketSenderPool = socketSenderPool;
        }

        public bool TryAddChannel( Socket socket, IDataPacker dataPacker, IDataUnpacker dataUnpacker, out int channelId )
        {
            channelId = Interlocked.Increment(ref ChannelIdSeed);

            var recvArgs = AcquireRecvArgs(dataUnpacker);

            if ( Channels.TryAdd(channelId, (socket, recvArgs, dataPacker)) )
            {
                FreeChannels.Add(channelId);
                BeginReceive(socket, recvArgs);

                return true;
            }
            else
            {
                ReleaseRecvArgs(recvArgs);

                return false;
            }
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
            if ( e.SocketError == SocketError.Success )
            {
                if ( e.BytesTransferred > 0 )
                {
                    var unpacker = (IDataUnpacker)e.UserToken;

                    var output = DataBufferPool.Acquire();

                    var unpackedData = unpacker.UnpackData(output, new ArraySegment<byte>(e.Buffer, e.Offset, e.BytesTransferred));

                    for ( int i = 0, offset = output.Offset; i < unpackedData.Length; i++ )
                    {
                        var (pairId, bytes) = unpackedData[i];
                        var sendBuffer = new ArraySegment<byte>(output.Array, offset, bytes);
                        var endpointSocket = EndPointStation.FindEndPoint(pairId);

                        var ss = SocketSenderPool.Acquire();
                        await ss.SendDataAsync(endpointSocket, sendBuffer);
                        SocketSenderPool.Release(ss);

                        offset += bytes;
                    }

                    DataBufferPool.Release(output);

                    BeginReceive(socket, e);
                }
            }
            // TODO: Error handling??
        }

        private SocketAsyncEventArgs AcquireRecvArgs( IDataUnpacker dataUnpacker )
        {
            var recvArgs = ReceiveEventArgsPool.AcquireWithBuffer();
            recvArgs.Completed += OnChannelSocketReceived;
            recvArgs.UserToken = dataUnpacker;

            return recvArgs;
        }

        private IDataUnpacker ReleaseRecvArgs( SocketAsyncEventArgs recvArgs )
        {
            recvArgs.Completed -= OnChannelSocketReceived;

            var dataUnpacker = (IDataUnpacker)recvArgs.UserToken;
            recvArgs.UserToken = null;

            ReceiveEventArgsPool.ReleaseWithBuffer(recvArgs);

            return dataUnpacker;
        }

        public bool TryRemoveChannel( int channelId, out Socket socket, out IDataPacker dataPacker, out IDataUnpacker dataUnpacker )
        {
            var result = Channels.TryRemove(channelId, out var channel);

            socket = channel.socket;
            dataPacker = channel.dataPacker;
            dataUnpacker = null;

            if ( result )
            {
                dataUnpacker = ReleaseRecvArgs(channel.recvArgs);
            }

            return result;
        }

        public async Task HandleEndPointReceivedDataAsync( int pairId, ArraySegment<byte> buffer )
        {
            var hasActivityInfo = EndpointActivities.TryGetValue(pairId, out var activityInfo);
            while ( !hasActivityInfo )
            {
                if ( FreeChannels.TryTake(out var freeChannelId) )
                {
                    if ( Channels.ContainsKey(freeChannelId) )
                    {
                        activityInfo = new EndpointActivityInfo { ChannelId = freeChannelId };
                        hasActivityInfo = EndpointActivities.TryAdd(pairId, activityInfo);
                    }
                }
                else
                {
                    // TODO: No free channels left! Need handle this depend on whether we're remote or local.
                }
            }

            if ( !Channels.TryGetValue(activityInfo.ChannelId, out var channel) )
            {
                // Channel just got removed right after we chose it, need to find another.

                EndpointActivities.TryRemove(pairId, out _);

                await HandleEndPointReceivedDataAsync(pairId, buffer);
                return;
            }

            var ss = SocketSenderPool.Acquire();
            var output = DataBufferPool.Acquire();

            int? recvId = Interlocked.Increment(ref activityInfo.ReceiveId);
            int bytes;
            while ( !channel.dataPacker.PackData(output, out bytes, pairId, recvId, buffer) )
            {
                await ss.SendDataAsync(channel.socket, output);

                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + bytes, buffer.Count - bytes);
                recvId = null;
            }
            await ss.SendDataAsync(channel.socket, new ArraySegment<byte>(output.Array, output.Offset, bytes));


            DataBufferPool.Release(output);
            SocketSenderPool.Release(ss);
        }

        class EndpointActivityInfo
        {
            public int ChannelId;
            public int ReceiveId;
        }
    }
}
