using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CnDream.Core
{
    public class DataPacker : IDataPacker
    {
        readonly ICryptoTransform Encryptor;
        readonly byte[] TempBuffer;

        public DataPacker( ICryptoTransform encryptor )
        {
            if ( !encryptor.CanTransformMultipleBlocks )
            {
                throw new NotSupportedException("Encryptor must be able to transform multiple blocks.");
            }

            Encryptor = encryptor;
            TempBuffer = new byte[GetTempBufferSize(encryptor)];
        }

        public int PackData( int pairId, int serialId, ArraySegment<byte> input, ArraySegment<byte> output )
        {
            var inArray = input.Array;
            var inStart = input.Offset;
            var inputCount = input.Count;

            var outArray = output.Array;
            var outStart = output.Offset;
            var maxBytesCanWrite = output.Count;

            var maxBytesCanTemp = TempBuffer.Length;

            var inputBlockSize = Encryptor.InputBlockSize;
            var outputBlockSize = Encryptor.OutputBlockSize;

            // Fill tempbuffer with metadata
            WritePrologue(TempBuffer, 0, out var tempWritten);
            WriteInt(pairId, TempBuffer, 0, ref tempWritten);
            WriteInt(serialId, TempBuffer, 0, ref tempWritten);
            WriteInt(inputCount, TempBuffer, 0, ref tempWritten);

            // Fill tempbuffer with payload if possible
            var bytesRead = 0;
            if ( tempWritten < maxBytesCanTemp )
            {
                var bytesCanTemp = maxBytesCanTemp - tempWritten;

                int bytesShouldRead, bytesShouldPad;
                if ( bytesCanTemp <= inputCount )
                {
                    bytesShouldRead = bytesCanTemp;
                    bytesShouldPad = 0;
                }
                else
                {
                    bytesShouldRead = inputCount;
                    bytesShouldPad = inputBlockSize - (inputCount % inputBlockSize);
                }

                Buffer.BlockCopy(inArray, inStart, TempBuffer, tempWritten, bytesShouldRead);

                bytesRead += bytesShouldRead;
                tempWritten += bytesShouldRead;

                WriteEpilogue(bytesShouldPad, TempBuffer, tempWritten);
            }

            // Transform bytes in TempBuffer
            var bytesWritten = Encryptor.TransformBlock(TempBuffer, 0, tempWritten, outArray, outStart);

            // Transform the remaining bytes, if any
            if ( bytesRead < inputCount )
            {
                var bytesRemaining = inputCount - bytesRead;
                var bytesShouldPad = inputBlockSize - (bytesRemaining % inputBlockSize);
                var bytesShouldRead = (bytesShouldPad == 0) ? bytesRemaining : bytesRemaining / inputBlockSize * inputBlockSize;

                bytesWritten += Encryptor.TransformBlock(inArray, inStart + bytesRead, bytesShouldRead, outArray, outStart + bytesWritten);
                bytesRead += bytesShouldRead;

                if ( bytesShouldPad > 0 )
                {
                    bytesShouldRead = inputBlockSize - bytesShouldPad;

                    Buffer.BlockCopy(inArray, inStart + bytesRead, TempBuffer, 0, bytesShouldRead);

                    bytesRead += bytesShouldRead;

                    WriteEpilogue(bytesShouldPad, TempBuffer, bytesShouldRead);

                    bytesWritten += Encryptor.TransformBlock(TempBuffer, 0, inputBlockSize, outArray, outStart + bytesWritten);
                }
            }

            Debug.Assert(bytesRead == inputCount);
            return bytesWritten;
        }

        public static int GetTempBufferSize( ICryptoTransform encryptor )
        {
            int size;
            for ( size = 0; size < 32; size += encryptor.InputBlockSize ) { }

            return size;
        }

        private static void WritePrologue( byte[] array, int offset, out int bytesWritten )
        {
            var rand = new Random();
            var limit = offset + rand.Next(4, 9);
            const int step = 10;

            for ( int i = offset, v = rand.Next(step); i < limit; i++, v += rand.Next(step) )
            {
                array[i] = (byte)v;
            }

            array[limit] = (byte)(array[limit - 1] - rand.Next(step));

            bytesWritten = limit + 1;
        }

        private static void WriteInt( int value, byte[] array, int offset, ref int bytesWritten )
        {
            offset += bytesWritten;

            array[offset++] = (byte)value;
            array[offset++] = (byte)(value >> 8);
            array[offset++] = (byte)(value >> 16);
            array[offset++] = (byte)(value >> 24);

            bytesWritten += 4;
        }

        private static int WriteEpilogue( int count, byte[] array, int offset )
        {
            var limit = offset + count;

            for ( int i = offset; i < limit; i++ )
            {
                array[i] = 0;
            }

            return count;
        }
    }
}
