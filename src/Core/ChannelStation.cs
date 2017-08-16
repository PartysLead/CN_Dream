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
        IPool<BufferedSocketAsyncEventArgs> ReceiveEventArgsPool;
        IDataPacker DataPacker;
        IDataUnpacker DataUnpacker;

        int ChannelIdSeed = 0;
        ConcurrentDictionary<int, (Socket socket, BufferedSocketAsyncEventArgs recvArgs)> Channels
            = new ConcurrentDictionary<int, (Socket, BufferedSocketAsyncEventArgs)>();
        ConcurrentDictionary<int, int> PairedChannels = new ConcurrentDictionary<int, int>();
        ConcurrentBag<int> FreeChannels = new ConcurrentBag<int>();

        public void Initialize( IEndPointStation endpointStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool, IDataPacker dataPacker, IDataUnpacker dataUnpacker )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            DataPacker = dataPacker;
            DataUnpacker = dataUnpacker;
        }

        public bool TryAddChannel( Socket channelSocket, out int channelId )
        {
            channelId = Interlocked.Increment(ref ChannelIdSeed);

            var recvArgs = AcquireRecvArgs();

            if ( Channels.TryAdd(channelId, (channelSocket, recvArgs)) )
            {
                FreeChannels.Add(channelId);
                BeginReceive(channelSocket, recvArgs);

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
                    ISocketSender ss = null; // TODO: buffer pool it

                    ArraySegment<byte> output;// TODO: buffer pool it
                    var pairId = DataUnpacker.UnpackData(output, e.Buffer, e.Offset, e.BytesTransferred);

                    ss.SetBuffer(output);
                    ss.SetSocket(EndPointStation.FindEndPoint(pairId));
                    await ss.SendDataAsync();

                    BeginReceive(socket, e);
                }
            }
            // TODO: Error handling??
        }

        private BufferedSocketAsyncEventArgs AcquireRecvArgs()
        {
            var recvArgs = ReceiveEventArgsPool.Acquire();
            recvArgs.Completed += OnChannelSocketReceived;
            return recvArgs;
        }

        private void ReleaseRecvArgs( BufferedSocketAsyncEventArgs recvArgs )
        {
            recvArgs.Completed -= OnChannelSocketReceived;
            ReceiveEventArgsPool.Release(recvArgs);
        }

        public bool TryRemoveChannel( int channelId, out Socket socket )
        {
            var result = Channels.TryRemove(channelId, out var channel);
            socket = channel.socket;

            if ( result )
            {
                ReleaseRecvArgs(channel.recvArgs);
            }

            return result;
        }

        public async Task HandleEndPointDataReceivedAsync( int pairId, byte[] buffer, int offset, int count )
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

                await HandleEndPointDataReceivedAsync(pairId, buffer, offset, count);
                return;
            }

            ISocketSender ss = null; // TODO: buffer pool it

            ArraySegment<byte> output;// TODO: buffer pool it
            DataPacker.PackData(output, pairId, wasPaired, buffer, offset, count);

            ss.SetBuffer(output);
            ss.SetSocket(channel.socket);
            await ss.SendDataAsync();
        }
    }
}
