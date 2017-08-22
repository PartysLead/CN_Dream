using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        int ChannelIdSeed = 0;
        ConcurrentDictionary<int, (Socket socket, SocketAsyncEventArgs recvArgs, IDataPacker dataPacker)> Channels
            = new ConcurrentDictionary<int, (Socket, SocketAsyncEventArgs, IDataPacker)>();
        ConcurrentDictionary<int, int> PairedChannels = new ConcurrentDictionary<int, int>();
        ConcurrentBag<int> FreeChannels = new ConcurrentBag<int>();

        public void Initialize( IEndPointStation endpointStation, ISocketAsyncEventArgsPool recvArgsPool, IPool<ArraySegment<byte>> dataBufferPool )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            DataBufferPool = dataBufferPool;
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

                        ISocketSender ss = null; // TODO: buffer pool it
                        await ss.SendDataAsync(endpointSocket, sendBuffer);

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
            var wasPaired = PairedChannels.TryGetValue(pairId, out var channelId);
            if ( !wasPaired )
            {
                var nowPaired = false;
                do
                {
                    if ( FreeChannels.TryTake(out var freeChannelId) )
                    {
                        if ( Channels.ContainsKey(freeChannelId) )
                        {
                            channelId = freeChannelId;
                            nowPaired = PairedChannels.TryAdd(pairId, channelId);
                        }
                    }
                    else
                    {
                        // TODO: No free channels left! Need handle this depend on whether we're remote or local.
                    }
                }
                while ( !nowPaired );
            }

            if ( !Channels.TryGetValue(channelId, out var channel) )
            {
                // Channel just got removed right after we choose it for pair, need to find another.

                PairedChannels.TryRemove(channelId, out _);

                await HandleEndPointReceivedDataAsync(pairId, buffer);
                return;
            }

            ISocketSender ss = null; // TODO: buffer pool it

            var output = DataBufferPool.Acquire();
            var bytesWritten = channel.dataPacker.PackData(output, pairId, wasPaired, buffer);
            
            await ss.SendDataAsync(channel.socket, new ArraySegment<byte>(output.Array, output.Offset, bytesWritten));

            DataBufferPool.Release(output);
        }
    }
}
