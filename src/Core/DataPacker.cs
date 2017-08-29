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

        public DataPacker( ICryptoTransform encrytor )
        {
            Encryptor = encrytor;
        }

        public bool PackData( ArraySegment<byte> output, out int bytesWritten, out int bytesRead, int pairId, int? serialId, ArraySegment<byte> input )
        {
            Debug.Assert(output.Count > Encryptor.OutputBlockSize);

            var outOffset = output.Offset;
            var bytesToRead = input.Count;

            byte[] tempBuffer = null;
            var tempBufferWritten = 0;

            if ( serialId.HasValue ) // the first portion
            {
                tempBuffer = new byte[Encryptor.InputBlockSize];

                WritePrologue(tempBuffer, out tempBufferWritten);
                WriteInt(serialId.Value, tempBuffer, ref tempBufferWritten);
                WriteInt(bytesToRead, tempBuffer, ref tempBufferWritten);

                if ( tempBufferWritten == tempBuffer.Length )
                {
                    outOffset += Encryptor.TransformBlock(tempBuffer, 0, tempBufferWritten, output.Array, outOffset);
                }
            }

            if ( tempBuffer != null && tempBufferWritten < tempBuffer.Length )
            {
                var bytesToCopy = Math.Min(tempBuffer.Length - tempBufferWritten, )
                Buffer.BlockCopy(input.Array, input.Offset, output.Array, outOffset, );

            }

            var freeSpace = output.Count - outOffset;

            if ( bytesToRead <= freeSpace )
            {
                freeSpace -= Encryptor.TransformBlock(input.Array, input.Offset, input.Count, output.Array, outOffset);
                bytes = output.Count - freeSpace;
                return true;
            }
            else
            {
                bytes = Encryptor.TransformBlock(input.Array, input.Offset, freeSpace, output.Array, outOffset);
                return false;
            }
        }

        private void WritePrologue( byte[] buffer, out int bytesWritten )
        {
            var rand = new Random();
            var length = rand.Next(4, 9);
            const int step = 10;

            for ( int i = 0, v = rand.Next(step); i < length; i++, v += rand.Next(step) )
            {
                buffer[i] = (byte)v;
            }

            buffer[length] = (byte)(buffer[length - 1] - rand.Next(step));

            bytesWritten = length + 1;
        }

        private void WriteInt( int value, byte[] buffer, ref int bytesWritten )
        {
            buffer[bytesWritten++] = (byte)value;
            buffer[bytesWritten++] = (byte)(value >> 8);
            buffer[bytesWritten++] = (byte)(value >> 16);
            buffer[bytesWritten++] = (byte)(value >> 24);
        }
    }
}
