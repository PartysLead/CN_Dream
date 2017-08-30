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
            Debug.Assert(output.Count >= 32 && output.Count > Encryptor.OutputBlockSize);

            var inArray = input.Array;
            var bytesToRead = input.Count;
            var inputBlockSize = Encryptor.InputBlockSize;

            var outArray = output.Array;
            var outStart = output.Offset;
            var outOffset = output.Offset;

            var tempArray = TempBuffer.Array;
            var tempStart = TempBuffer.Offset;
            var tempWritten = 0;

            if ( serialId.HasValue ) // the first portion
            {
                WritePrologue(tempArray, tempStart, out tempWritten);
                WriteInt(pairId, tempArray, tempStart, ref tempWritten);
                WriteInt(serialId.Value, tempArray, tempStart, ref tempWritten);
                WriteInt(bytesToRead, tempArray, tempStart, ref tempWritten);

                if ( tempWritten == TempBuffer.Count )
                {
                    outOffset += Encryptor.TransformBlock(tempArray, 0, tempStart + tempWritten, outArray, outOffset);
                    Debug.Assert(outOffset < output.Offset + output.Count);

                    tempWritten = 0;
                }

                bytesWritten = outOffset - outStart;
            }

            bytesRead = 0;

            if ( tempWritten > 0 && tempWritten < TempBuffer.Count )
            {
                bytesRead = Math.Min(TempBuffer.Count - tempWritten, bytesToRead);

                Buffer.BlockCopy(inArray, input.Offset, tempArray, tempWritten, bytesRead);

                tempWritten += bytesRead;

                var padding = tempWritten % inputBlockSize;
                if ( padding > 0 )
                {
                    Debug.Assert(bytesRead == bytesToRead);

                    padding = inputBlockSize - padding;
                    WritePadding(padding, tempArray, tempStart, ref tempWritten);
                }

                outOffset += Encryptor.TransformBlock(tempArray, tempStart, tempWritten, outArray, outOffset);
                Debug.Assert(outOffset < output.Offset + output.Count);

                tempWritten = 0;
                bytesWritten = outOffset - outStart;

                if ( padding > 0 )
                {
                    return true;
                }
            }

            Debug.Assert(tempWritten == 0);

            var blocksCanRead = Math.Min((bytesToRead - bytesRead) / inputBlockSize, (output.Count - outOffset) / Encryptor.OutputBlockSize);

            if ( blocksCanRead == 0 )
            {
                tempWritten = bytesToRead - bytesRead;
                Buffer.BlockCopy(inArray, input.Offset + bytesRead, tempArray, 0, tempWritten);
                bytesRead = bytesToRead;

                WritePadding(inputBlockSize - (tempWritten % inputBlockSize), tempArray, tempStart, ref tempWritten);

                outOffset += Encryptor.TransformBlock(tempArray, tempStart, tempWritten, outArray, outOffset);
                bytesWritten = outOffset - outStart;

                return true;
            }
            else
            {
                var bytesCanRead = inputBlockSize * blocksCanRead;
                outOffset += Encryptor.TransformBlock(inArray, input.Offset + bytesRead, bytesCanRead, outArray, outOffset);
                bytesRead += bytesCanRead;
                bytesWritten = outOffset - outStart;

                return bytesRead == bytesToRead;
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

        private static void WritePadding( int padding, byte[] array, int offset, ref int bytesWritten )
        {
            throw new NotImplementedException();
        }
    }
}
