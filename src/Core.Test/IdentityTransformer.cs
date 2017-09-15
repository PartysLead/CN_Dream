using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace CnDream.Core.Test
{
    class IdentityTransformer : ICryptoTransform
    {
        public bool CanReuseTransform => true;

        public bool CanTransformMultipleBlocks => true;

        public int InputBlockSize => 16;

        public int OutputBlockSize => 16;

        public int TransformBlock( byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset )
        {
            if ( inputCount % InputBlockSize != 0 )
            {
                throw new ArgumentException("inputCount should be multiple of input block size!", nameof(inputCount));
            }

            var blocksToTransform = inputCount / InputBlockSize;
            var bytesWritten = blocksToTransform * OutputBlockSize;

            if ( outputOffset + bytesWritten > outputBuffer.Length )
            {
                throw new ArgumentException("Attempt to transform too many blocks!");
            }

            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, bytesWritten);

            return bytesWritten;
        }

        public byte[] TransformFinalBlock( byte[] inputBuffer, int inputOffset, int inputCount ) => throw new NotImplementedException();

        public void Dispose() { }
    }
}
