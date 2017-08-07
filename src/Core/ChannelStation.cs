using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class ChannelStation : IChannelStation
    {
        readonly IEndPointStation EndPointStation;
        readonly IPool<BufferedSocketAsyncEventArgs> ReceiveEventArgsPool;
        readonly IDataSenderProvider TransformerProvider;

        const int IdlePairId = 0;
        const int ChannelSocketBucketSize = 128;
        int[] ChannelSocketPairIds = new int[ChannelSocketBucketSize];
        Socket[] ChannelSockets = new Socket[ChannelSocketBucketSize];

        public ChannelStation( IEndPointStation endpointStation, IPool<BufferedSocketAsyncEventArgs> recvArgsPool, IDataSenderProvider transformerProvider )
        {
            EndPointStation = endpointStation;
            ReceiveEventArgsPool = recvArgsPool;
            TransformerProvider = transformerProvider;
        }

        public void AddChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSockets[i], channelSocket, null) == null )
                {
                    ChannelSocketPairIds[i] = IdlePairId;

                    // TODO: Hook up things.
                    break;
                }
            }

            throw new NotImplementedException("Cannot handle adding more channels for now..");
        }

        public void RemoveChannel( Socket channelSocket )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSockets[i], null, channelSocket) == channelSocket )
                {
                    // TODO: Un-hook things.
                    break;
                }
            }

            throw new ArgumentException("BUG: Removing a channel that is not added!", nameof(channelSocket));
        }

        public IDataSender GetTransformer( int pairId )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSocketPairIds[i], pairId, IdlePairId) == IdlePairId )
                {
                    var socket = ChannelSockets[i];
                    if ( socket != null )
                    {
                        return TransformerProvider.ProvideDataSender(pairId, socket);
                    }
                }
            }

            // TODO: ????
            return null;
        }

        public void ReturnTransformer( int pairId, IDataSender transformer )
        {
            for ( int i = 0; i < ChannelSocketBucketSize; i++ )
            {
                if ( Interlocked.CompareExchange(ref ChannelSocketPairIds[i], IdlePairId, pairId) == pairId )
                {
                    var socket = ChannelSockets[i];
                    if ( socket != null )
                    {
                        return;
                    }
                }
            }

            throw new ArgumentException("BUG: Marking a non-existent channel as idle!", nameof(pairId));
        }
    }
}
