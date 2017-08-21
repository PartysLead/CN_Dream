using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace CnDream.Core
{
    public class BufferPool : Pool<ArraySegment<byte>>
    {
        readonly int PerAcquireSize;
        readonly int CapacityGrowDelta;

        int ActiveCapacity;
        byte[] ActiveChunk;

        public BufferPool( int perAcquireSizeInKB, int initialChunkCapacity, int capacityGrowDelta )
        {
            PerAcquireSize = perAcquireSizeInKB * 1024;
            CapacityGrowDelta = capacityGrowDelta;
            ActiveCapacity = initialChunkCapacity;
            TryInitialize(initialChunkCapacity);
        }

        private bool TryInitialize( int capacity )
        {
            lock ( this ) // serialize multiple calls
            {
                if ( FreeObjects.IsEmpty )
                {
                    var newChunk = new byte[PerAcquireSize * capacity];
                    FreeObjects = new ConcurrentBag<ArraySegment<byte>>(GenerateArraySegments(newChunk, capacity));
                    ActiveChunk = newChunk;

                    return true;
                }
            }

            return false;
        }

        private IEnumerable<ArraySegment<byte>> GenerateArraySegments( byte[] newChunk, int capacity )
        {
            var result = new ArraySegment<byte>[capacity];

            for ( int i = 0, offset = 0; i < capacity; i++, offset += PerAcquireSize )
            {
                result[i] = new ArraySegment<byte>(newChunk, offset, PerAcquireSize);
            }

            return result;
        }

        protected override bool CreateObject( out ArraySegment<byte> obj )
        {
            var newCap = ActiveCapacity + CapacityGrowDelta; // We don't want the number grow for each thread
            if ( TryInitialize(newCap) )
            {
                Interlocked.Exchange(ref ActiveCapacity, newCap);
            }

            obj = Acquire();
            return true;
        }

        public override void Release( ArraySegment<byte> t )
        {
            if ( t.Array == ActiveChunk )
            {
                base.Release(t);
            }
        }
    }
}
