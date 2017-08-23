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
        ConcurrentDictionary<int, EndpointState> EndpointStates = new ConcurrentDictionary<int, EndpointState>();
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

                    var input = new ArraySegment<byte>(e.Buffer, e.Offset, e.BytesTransferred);
                    while ( unpacker.UnpackData(output, out var description, input) )
                    {
                        var pairId = description.pairId.Value;
                        var serialId = description.serialId.Value;
                        var totalLength = description.totalLength.Value;
                        var bytesRead = description.bytesRead;

                        var unpackedData = new ArraySegment<byte>(output.Array, output.Offset, description.bytesWritten);
                        var endpointSocket = EndPointStation.FindEndPoint(pairId);

                        await EndpointStates[pairId].BufferAndSendAvailableContentsAsync(endpointSocket, serialId, totalLength, unpackedData);

                        if ( bytesRead == input.Count )
                        {
                            break;
                        }

                        input = new ArraySegment<byte>(input.Array, input.Offset + bytesRead, input.Count - bytesRead);
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
            var endpointState = EndpointStates.GetOrAdd(pairId, _ => new EndpointState());

            if ( !FreeChannels.TryTake(out var freeChannelId) )
            {
                // TODO: No free channels left! Need handle this depend on whether we're remote or local.
            }

            if ( !Channels.TryGetValue(freeChannelId, out var channel) )
            {
                // Channel was removed, but FreeChannels is ConcurrentBag so could not remove a specific item, just find another.
                await HandleEndPointReceivedDataAsync(pairId, buffer);
                return;
            }

            var ss = SocketSenderPool.Acquire();
            var output = DataBufferPool.Acquire();

            int? serialId = Interlocked.Increment(ref endpointState.PackingSerialId);
            int bytes;
            while ( !channel.dataPacker.PackData(output, out bytes, pairId, serialId, buffer) )
            {
                await ss.SendDataAsync(channel.socket, output);

                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + bytes, buffer.Count - bytes);
                serialId = null;
            }
            await ss.SendDataAsync(channel.socket, new ArraySegment<byte>(output.Array, output.Offset, bytes));


            DataBufferPool.Release(output);
            SocketSenderPool.Release(ss);
        }

        class EndpointState
        {
            public int PackingSerialId;

            int UnpackingSerialId = 1;
            int UnpackingBytesLeft;
            ConcurrentDictionary<int, (ArraySegment<byte> buffer, int bytesWritten)> UnpackedDataBuffers = new ConcurrentDictionary<int, (ArraySegment<byte>, int)>();

            public async Task BufferAndSendAvailableContentsAsync( Socket socket, int serialId, int totalLength, ArraySegment<byte> data )
            {
                ISocketSender sender = null;

                if ( serialId == UnpackingSerialId )
                {
                    var bytesLeft = UnpackingBytesLeft;
                    if ( bytesLeft == 0 )
                    {
                        bytesLeft = totalLength;
                    }

                    await sender.SendDataAsync(socket, data);

                    bytesLeft -= data.Count;
                    Interlocked.Exchange(ref UnpackingBytesLeft, bytesLeft);

                    if ( bytesLeft == 0 )
                    {
                        var nextSerialId = Interlocked.Increment(ref UnpackingSerialId);
                    }
                }

            }
        }
    }
}
