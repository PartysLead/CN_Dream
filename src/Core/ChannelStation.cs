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
        Func<int, EndpointState> EndpointStateFactory;
        ConcurrentBag<int> FreeChannels = new ConcurrentBag<int>();

        public void Initialize( IEndPointStation endpointStation, ISocketAsyncEventArgsPool recvArgsPool, IPool<ArraySegment<byte>> dataBufferPool, IPool<ISocketSender> socketSenderPool )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            DataBufferPool = dataBufferPool;
            SocketSenderPool = socketSenderPool;
            EndpointStateFactory = _ => new EndpointState(DataBufferPool, SocketSenderPool);
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
                        var payloadSize = description.payloadSize.Value;
                        var bytesRead = description.bytesRead;

                        var unpackedData = new ArraySegment<byte>(output.Array, output.Offset, description.bytesWritten);
                        var endpointSocket = EndPointStation.FindEndPoint(pairId);

                        await EndpointStates[pairId].BufferAndSendAvailableContentsAsync(endpointSocket, serialId, payloadSize, unpackedData);

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
            var endpointState = EndpointStates.GetOrAdd(pairId, EndpointStateFactory);

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
            int bytesWritten;
            while ( !channel.dataPacker.PackData(output, out bytesWritten, out var bytesRead, pairId, serialId, buffer) )
            {
                await ss.SendDataAsync(channel.socket, new ArraySegment<byte>(output.Array, output.Offset, bytesWritten));

                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + bytesRead, buffer.Count - bytesRead);
                serialId = null;
            }
            await ss.SendDataAsync(channel.socket, new ArraySegment<byte>(output.Array, output.Offset, bytesWritten));


            DataBufferPool.Release(output);
            SocketSenderPool.Release(ss);
        }

        class EndpointState
        {
            IPool<ArraySegment<byte>> BufferPool;
            IPool<ISocketSender> SocketSenderPool;
            public int PackingSerialId;

            volatile int UnpackingSerialId = 1;
            ConcurrentDictionary<int, EndpointSendBufferState> UnpackedDataBuffers = new ConcurrentDictionary<int, EndpointSendBufferState>();
            Func<int, EndpointSendBufferState> EndPointSendBufferStateFactory;

            public EndpointState( IPool<ArraySegment<byte>> bufferPool, IPool<ISocketSender> socketSenderPool )
            {
                BufferPool = bufferPool;
                SocketSenderPool = socketSenderPool;
                EndPointSendBufferStateFactory = _ => new EndpointSendBufferState(BufferPool.Acquire());
            }

            public async Task BufferAndSendAvailableContentsAsync( Socket endpointSocket, int serialId, int payloadSize, ArraySegment<byte> data )
            {
                var sendBufferState = UnpackedDataBuffers.GetOrAdd(serialId, EndPointSendBufferStateFactory);

                // for the same serialId, this function is called serializely
                if ( serialId == UnpackingSerialId )
                {
                    await SendCurrentAndMaybeNextReadyBuffersAsync(endpointSocket, serialId, payloadSize, data, sendBufferState);
                }
                else
                {
                    var buffer = sendBufferState.Buffer;
                    var bytesInBuffer = sendBufferState.BytesInBuffer;
                    var newBytes = data.Count;

                    Debug.Assert(sendBufferState.BytesSent == 0);

                    if ( bytesInBuffer + newBytes == payloadSize )
                    {
                        lock ( this )
                        {
                            sendBufferState.Ready = true;
                            if ( serialId != UnpackingSerialId )
                            {
                                return;
                            }
                        }

                        await SendCurrentAndMaybeNextReadyBuffersAsync(endpointSocket, serialId, payloadSize, data, sendBufferState);
                    }
                    else
                    {
                        Buffer.BlockCopy(data.Array, data.Offset, buffer.Array, bytesInBuffer, newBytes);

                        sendBufferState.BytesInBuffer += bytesInBuffer;
                    }
                }
            }

            private async Task SendCurrentAndMaybeNextReadyBuffersAsync( Socket endpointSocket, int serialId, int payloadSize, ArraySegment<byte> data, EndpointSendBufferState sendBufferState )
            {
                var sender = SocketSenderPool.Acquire();

                var bytesSent = sendBufferState.BytesSent;
                var bytesInBuffer = sendBufferState.BytesInBuffer;
                if ( bytesInBuffer > 0 )
                {
                    var sendbuffer = sendBufferState.Buffer;

                    await sender.SendDataAsync(endpointSocket, new ArraySegment<byte>(sendbuffer.Array, sendbuffer.Offset, bytesInBuffer));
                    bytesSent += bytesInBuffer;

                    sendBufferState.BytesInBuffer = 0;
                }

                await sender.SendDataAsync(endpointSocket, data);
                bytesSent += data.Count;
                sendBufferState.BytesSent = bytesSent;

                if ( bytesSent == payloadSize )
                {
                    if ( UnpackedDataBuffers.TryRemove(serialId, out _) )
                    {
                        BufferPool.Release(sendBufferState.Buffer);
                    }

                    await SendNextReadyBuffersAsync(endpointSocket, serialId, sender);
                }

                SocketSenderPool.Release(sender);
            }

            private async Task SendNextReadyBuffersAsync( Socket endpointSocket, int serialId, ISocketSender sender )
            {
                while ( true )
                {
                    EndpointSendBufferState sendBufferState;
                    lock ( this )
                    {
                        if ( !UnpackedDataBuffers.TryGetValue(++serialId, out sendBufferState) || !sendBufferState.Ready )
                        {
                            UnpackingSerialId = serialId;
                            break;
                        }
                    }

                    var sendbuffer = sendBufferState.Buffer;
                    await sender.SendDataAsync(endpointSocket, new ArraySegment<byte>(sendbuffer.Array, sendbuffer.Offset, sendBufferState.BytesInBuffer));

                    UnpackedDataBuffers.TryRemove(serialId, out _);
                    BufferPool.Release(sendbuffer);
                }
            }

            class EndpointSendBufferState
            {
                public ArraySegment<byte> Buffer;
                public bool Ready;
                public int PayloadSize;
                public int BytesSent;
                public int BytesInBuffer;

                public EndpointSendBufferState( ArraySegment<byte> buffer )
                {
                    Buffer = buffer;
                }
            }
        }
    }
}
