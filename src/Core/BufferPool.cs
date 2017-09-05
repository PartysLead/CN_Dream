using System;
using System.Collections.Generic;

namespace CnDream.Core
{
    public class BufferPool : Pool<ArraySegment<byte>>
    {
        readonly int PerAcquireSize;
        byte[] BuffersBed;

        public BufferPool( int perAcquireSizeInKB, int buffersInPool )
        {
            PerAcquireSize = perAcquireSizeInKB * 1024;
            BuffersBed = new byte[PerAcquireSize * buffersInPool];
            var buffers = GenerateArraySegments(buffersInPool);
        }

        private IEnumerable<ArraySegment<byte>> GenerateArraySegments( int buffersInPool )
        {
            var result = new ArraySegment<byte>[buffersInPool];

            for ( int i = 0, offset = 0; i < buffersInPool; i++, offset += PerAcquireSize )
            {
                result[i] = new ArraySegment<byte>(BuffersBed, offset, PerAcquireSize);
            }

            return result;
        }

        protected override ArraySegment<byte> CreateObject()
        {
            var array = new byte[PerAcquireSize];
            return new ArraySegment<byte>(array, 0, array.Length);
        }

        public override void Release( ArraySegment<byte> t )
        {
            if ( t.Array == BuffersBed )
            {
                base.Release(t);
            }
        }
    }
}
