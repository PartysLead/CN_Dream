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
        readonly ArraySegment<byte> TempBuffer;

        public DataPacker( ICryptoTransform encryptor, ArraySegment<byte> tempBuffer )
        {
            if ( !encryptor.CanTransformMultipleBlocks )
            {
                throw new NotSupportedException("Encryptor must be able to transform multiple blocks.");
            }

            if ( tempBuffer.Count != GetTempBufferSize(encryptor) )
            {
                throw new ArgumentException("TempBuffer does not has expected size.", nameof(tempBuffer));
            }

            Encryptor = encryptor;
            TempBuffer = tempBuffer;
        }

        public bool PackData( ArraySegment<byte> output, out int bytesWritten, out int bytesRead, int pairId, int? serialId, ArraySegment<byte> input )
        {
            var inArray = input.Array;
            var inStart = input.Offset;
            var maxBytesToRead = input.Count;

            var outArray = output.Array;
            var outStart = output.Offset;
            var outOffset = outStart;
            var maxBytesCanWrite = output.Count;

            var tempArray = TempBuffer.Array;
            var tempStart = TempBuffer.Offset;
            var maxBytesCanTemp = TempBuffer.Count;
            var tempWritten = 0;

            var inputBlockSize = Encryptor.InputBlockSize;
            var outputBlockSize = Encryptor.OutputBlockSize;

            Debug.Assert(maxBytesCanWrite >= 32 && maxBytesCanWrite >= outputBlockSize);

            if ( serialId.HasValue ) // the first portion
            {
                WritePrologue(tempArray, tempStart, out tempWritten);
                WriteInt(pairId, tempArray, tempStart, ref tempWritten);
                WriteInt(serialId.Value, tempArray, tempStart, ref tempWritten);
                WriteInt(maxBytesToRead, tempArray, tempStart, ref tempWritten);

                if ( tempWritten == maxBytesCanTemp )
                {
                    outOffset += Encryptor.TransformBlock(tempArray, 0, tempStart + tempWritten, outArray, outOffset);
                    Debug.Assert(outOffset < outStart + maxBytesCanWrite);

                    tempWritten = 0;
                }

                bytesWritten = outOffset - outStart;
            }

            bytesRead = 0;

            if ( tempWritten > 0 && tempWritten < maxBytesCanTemp )
            {
                bytesRead = Math.Min(maxBytesCanTemp - tempWritten, maxBytesToRead);

                Buffer.BlockCopy(inArray, inStart, tempArray, tempWritten, bytesRead);

                tempWritten += bytesRead;

                var padding = tempWritten % inputBlockSize;
                if ( padding > 0 )
                {
                    Debug.Assert(bytesRead == maxBytesToRead);

                    padding = inputBlockSize - padding;
                    tempWritten += WriteGarbage(padding, tempArray, tempStart + tempWritten);
                }

                outOffset += Encryptor.TransformBlock(tempArray, tempStart, tempWritten, outArray, outOffset);
                Debug.Assert(outOffset < outStart + maxBytesCanWrite);

                tempWritten = 0;
                bytesWritten = outOffset - outStart;

                if ( padding > 0 )
                {
                    return true;
                }
            }

            Debug.Assert(tempWritten == 0);

            var blocksCanRead = Math.Min((maxBytesToRead - bytesRead) / inputBlockSize, (maxBytesCanWrite - outOffset) / outputBlockSize);

            if ( blocksCanRead == 0 )
            {
                tempWritten = maxBytesToRead - bytesRead;
                Buffer.BlockCopy(inArray, inStart + bytesRead, tempArray, 0, tempWritten);
                bytesRead = maxBytesToRead;

                tempWritten += WriteGarbage(inputBlockSize - (tempWritten % inputBlockSize), tempArray, tempStart + tempWritten);

                outOffset += Encryptor.TransformBlock(tempArray, tempStart, tempWritten, outArray, outOffset);
                bytesWritten = outOffset - outStart;

                return true;
            }
            else
            {
                var bytesCanRead = inputBlockSize * blocksCanRead;
                outOffset += Encryptor.TransformBlock(inArray, inStart + bytesRead, bytesCanRead, outArray, outOffset);
                bytesRead += bytesCanRead;
                bytesWritten = outOffset - outStart;

                return bytesRead == maxBytesToRead;
            }
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

        private static int WriteGarbage( int count, byte[] array, int offset )
        {
            var rand = new Random();
            var limit = offset + count;

            for ( int i = offset; i < limit; i++ )
            {
                array[i] = (byte)rand.Next(0, 256);
            }

            return count;
        }
    }
}
