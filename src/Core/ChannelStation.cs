using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public abstract class ChannelStation : IChannelStation
    {
        IEndPointStation _EndPointStation;
        protected IEndPointStation EndPointStation => _EndPointStation;

        ISocketAsyncEventArgsPool ReceiveEventArgsPool;
        IPool<ArraySegment<byte>> DataBufferPool;
        IPool<ISocketSender> SocketSenderPool;

        int ChannelIdSeed = 0;
        ConcurrentDictionary<int, ChannelDescription> Channels = new ConcurrentDictionary<int, ChannelDescription>();
        ConcurrentDictionary<int, EndpointState> EndpointStates = new ConcurrentDictionary<int, EndpointState>();
        Func<int, EndpointState> EndpointStateFactory;
        Func<string, Task> MessageHandlerFunc;

        protected readonly ConcurrentBag<int> FreeChannels = new ConcurrentBag<int>();

        public void Initialize( IEndPointStation endpointStation, ISocketAsyncEventArgsPool recvArgsPool, IPool<ArraySegment<byte>> dataBufferPool, IPool<ISocketSender> socketSenderPool )
        {
            _EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            DataBufferPool = dataBufferPool;
            SocketSenderPool = socketSenderPool;
            EndpointStateFactory = _ => new EndpointState(DataBufferPool, SocketSenderPool);
            MessageHandlerFunc = HandleReceivedMessageAsync;
        }

        public int AddChannel( Socket socket, IDataPacker dataPacker, IDataUnpacker dataUnpacker )
        {
            var channelId = Interlocked.Increment(ref ChannelIdSeed);
            if ( channelId == 0 )
            {
                channelId = Interlocked.Increment(ref ChannelIdSeed);
            }

            var recvArgs = AcquireRecvArgs(dataUnpacker);

            if ( Channels.TryAdd(channelId, new ChannelDescription(channelId, socket, recvArgs, dataPacker)) )
            {
                FreeChannels.Add(channelId);
                BeginReceive(socket, recvArgs);

                return channelId;
            }
            else
            {
                ReleaseRecvArgs(recvArgs);

                return -1;
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
                    using ( var pooledOutputBuffer = DataBufferPool.GetPooledObject() )
                    {
                        var unpacker = (IDataUnpacker)e.UserToken;
                        var output = pooledOutputBuffer.Value;
                        var input = new ArraySegment<byte>(e.Buffer, e.Offset, e.BytesTransferred);

                        while ( unpacker.UnpackData(output, out var bytesWritten, out var bytesRead,
                                                    out var pairId, out var serialId, out var payloadSize, input) )
                        {
                            var unpackedData = new ArraySegment<byte>(output.Array, output.Offset, bytesWritten);
                            var endpointState = EndpointStates.GetOrAdd(pairId, EndpointStateFactory);

                            if ( pairId == 0 )
                            {
                                await endpointState.FireAvailableMessagesAsync(serialId, payloadSize, unpackedData, MessageHandlerFunc);
                            }
                            else
                            {
                                var endpointSocket = EndPointStation.FindEndPoint(pairId);
                                await endpointState.SendAvailableContentsAsync(endpointSocket, serialId, payloadSize, unpackedData);
                            }

                            input = new ArraySegment<byte>(input.Array, input.Offset + bytesRead, input.Count - bytesRead);
                        }
                    }

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

        public void RemoveChannel( int channelId )
        {
            if ( Channels.TryRemove(channelId, out var channel) )
            {
                ReleaseRecvArgs(channel.RecvArgs);
            }
        }

        public async Task HandleEndPointReceivedDataAsync( int pairId, ArraySegment<byte> buffer )
        {
            var channel = await FindFreeChannel();

            using ( var pooledSocketSender = SocketSenderPool.GetPooledObject() )
            using ( var pooledOutputBuffer = DataBufferPool.GetPooledObject() )
            {
                var sender = pooledSocketSender.Value;
                var output = pooledOutputBuffer.Value;

                var endpointState = EndpointStates.GetOrAdd(pairId, EndpointStateFactory);
                var serialId = Interlocked.Increment(ref endpointState.PackingSerialId);

                var bytesWritten = channel.DataPacker.PackData(pairId, serialId, buffer, output);
                await sender.SendDataAsync(channel.Socket, output.Array, output.Offset, bytesWritten);

                FreeChannels.Add(channel.Id);
            }
        }

        private async Task<ChannelDescription> FindFreeChannel()
        {
            ChannelDescription channel;
            int freeChannelId;
            do
            {
                if ( !FreeChannels.TryTake(out freeChannelId) )
                {
                    freeChannelId = await CreateFreeChannelAsync();
                }
            }
            while ( !Channels.TryGetValue(freeChannelId, out channel) );

            return channel;
        }

        protected abstract Task<int> CreateFreeChannelAsync();

        protected abstract Task HandleReceivedMessageAsync( string message );

        public Task SendMessageAsync( string message )
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            return HandleEndPointReceivedDataAsync(0, new ArraySegment<byte>(bytes, 0, bytes.Length));
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

            public async Task FireAvailableMessagesAsync( int serialId, int payloadSize, ArraySegment<byte> data, Func<string, Task> messageHandler )
            {
                var sendBufferState = UnpackedDataBuffers.GetOrAdd(serialId, EndPointSendBufferStateFactory);
                var buffer = sendBufferState.Buffer;

                Buffer.BlockCopy(data.Array, data.Offset, buffer.Array, buffer.Offset + sendBufferState.BytesInBuffer, data.Count);
                sendBufferState.BytesInBuffer += data.Count;

                if ( serialId == UnpackingSerialId && sendBufferState.BytesInBuffer == payloadSize )
                {
                    await messageHandler(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, payloadSize));
                    RemoveAndReleaseBuffers(serialId);
                }

                throw new NotImplementedException();
            }

            public async Task SendAvailableContentsAsync( Socket endpointSocket, int serialId, int payloadSize, ArraySegment<byte> data )
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
                        Buffer.BlockCopy(data.Array, data.Offset, buffer.Array, buffer.Offset + bytesInBuffer, newBytes);

                        sendBufferState.BytesInBuffer += bytesInBuffer;
                    }
                }
            }

            private async Task SendCurrentAndMaybeNextReadyBuffersAsync( Socket endpointSocket, int serialId, int payloadSize, ArraySegment<byte> data, EndpointSendBufferState sendBufferState )
            {
                using ( var pooledSender = SocketSenderPool.GetPooledObject() )
                {
                    var sender = pooledSender.Value;

                    var bytesSent = sendBufferState.BytesSent;
                    var bytesInBuffer = sendBufferState.BytesInBuffer;
                    if ( bytesInBuffer > 0 )
                    {
                        var sendbuffer = sendBufferState.Buffer;

                        await sender.SendDataAsync(endpointSocket, sendbuffer.Array, sendbuffer.Offset, bytesInBuffer);
                        bytesSent += bytesInBuffer;

                        sendBufferState.BytesInBuffer = 0;
                    }

                    await sender.SendDataAsync(endpointSocket, data.Array, data.Offset, data.Count);
                    bytesSent += data.Count;
                    sendBufferState.BytesSent = bytesSent;

                    if ( bytesSent == payloadSize )
                    {
                        RemoveAndReleaseBuffers(serialId);

                        await SendNextReadyBuffersAsync(endpointSocket, serialId, sender);
                    }
                }
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
                    await sender.SendDataAsync(endpointSocket, sendbuffer.Array, sendbuffer.Offset, sendBufferState.BytesInBuffer);

                    RemoveAndReleaseBuffers(serialId);
                }
            }

            private void RemoveAndReleaseBuffers( int serialId )
            {
                if ( UnpackedDataBuffers.TryRemove(serialId, out var state) )
                {
                    BufferPool.Release(state.Buffer);
                }
            }

            class EndpointSendBufferState
            {
                public ArraySegment<byte> Buffer;
                public bool Ready;
                public int BytesSent;
                public int BytesInBuffer;

                public EndpointSendBufferState( ArraySegment<byte> buffer )
                {
                    Buffer = buffer;
                }
            }
        }

        struct ChannelDescription
        {
            public readonly int Id;
            public readonly Socket Socket;
            public readonly SocketAsyncEventArgs RecvArgs;
            public readonly IDataPacker DataPacker;

            public ChannelDescription( int id, Socket socket, SocketAsyncEventArgs recvArgs, IDataPacker dataPacker )
            {
                Id = id;
                Socket = socket;
                RecvArgs = recvArgs;
                DataPacker = dataPacker;
            }
        }
    }
}
