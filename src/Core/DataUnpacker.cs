using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CnDream.Core
{
    public class DataUnpacker : IDataUnpacker
    {
        readonly ICryptoTransform Decryptor;
        readonly byte[] EncryptedBytes, DecryptedBytes;
        int EncryptedCount, DecryptedCount, MaxBytesCanTemp;
        int PairId, SerialId, PayloadSize, PayloadWritten;
        bool HeaderRead;

        public DataUnpacker( ICryptoTransform decryptor )
        {
            Decryptor = decryptor;
            MaxBytesCanTemp = DataPacker.GetTempBufferSize(decryptor);
            EncryptedBytes = new byte[MaxBytesCanTemp];
            DecryptedBytes = new byte[MaxBytesCanTemp];
        }

        public bool UnpackData
        (
            ArraySegment<byte> output,
            out int bytesWritten, out int bytesRead,
            out int pairId, out int serialId, out int payloadSize,
            ArraySegment<byte> input
        )
        {
            var inArray = input.Array;
            var inStart = input.Offset;
            var maxBytesCanRead = input.Count;

            var outArray = output.Array;
            var outStart = output.Offset;
            var maxBytesCanWrite = output.Count;

            var inputBlockSize = Decryptor.InputBlockSize;
            var outputBlockSize = Decryptor.OutputBlockSize;

            bytesWritten = 0;
            bytesRead = 0;

            if ( PayloadWritten == 0 )
            {
                if ( !HeaderRead && DecryptedCount > 0 )
                {
                    HeaderRead = TryReadHeader();
                }

                while ( !HeaderRead && bytesRead < maxBytesCanRead )
                {
                    // Read input block by block and try get the header out.

                    Debug.Assert(EncryptedCount == 0 || EncryptedCount % inputBlockSize != 0);

                    var minNeededBytes = inputBlockSize - (EncryptedCount % inputBlockSize);
                    Debug.Assert(minNeededBytes > 0);

                    var bytesToRead = Math.Min(minNeededBytes, Math.Min(MaxBytesCanTemp - EncryptedCount, maxBytesCanRead - bytesRead));
                    Buffer.BlockCopy(inArray, inStart + bytesRead, EncryptedBytes, EncryptedCount, bytesToRead);

                    bytesRead += bytesToRead;
                    EncryptedCount += bytesToRead;

                    if ( EncryptedCount % inputBlockSize == 0 )
                    {
                        DecryptedCount += Decryptor.TransformBlock(EncryptedBytes, 0, EncryptedCount, DecryptedBytes, DecryptedCount);
                        EncryptedCount = 0;

                        HeaderRead = TryReadHeader();
                    }
                    else
                    {
                        break;
                    }
                }

                if ( !HeaderRead )
                {
                    Debug.Assert(bytesRead == maxBytesCanRead);

                    pairId = serialId = payloadSize = -1;

                    return false;
                }
            }

            Debug.Assert(HeaderRead);

            pairId = PairId;
            serialId = SerialId;
            payloadSize = PayloadSize;

            if ( DecryptedCount > 0 )
            {
                Debug.Assert(bytesWritten == 0);

                bytesWritten = Math.Min(payloadSize - PayloadWritten, Math.Min(DecryptedCount, maxBytesCanWrite));
                PayloadWritten += bytesWritten;

                Buffer.BlockCopy(DecryptedBytes, 0, outArray, outStart, bytesWritten);

                var newDeTempSize = DecryptedCount - bytesWritten;
                if ( newDeTempSize > 0 )
                {
                    Buffer.BlockCopy(DecryptedBytes, bytesWritten, DecryptedBytes, 0, newDeTempSize);
                }

                DecryptedCount = newDeTempSize;

                if ( bytesWritten < DecryptedCount )
                {
                    if ( PayloadWritten == payloadSize )
                    {
                        HeaderRead = false;
                        PayloadWritten = 0;
                    }

                    return true;
                }
            }

            Debug.Assert(DecryptedCount == 0);

            var payloadLeft = payloadSize - PayloadWritten;
            var outputBlocksLeft = (maxBytesCanWrite - bytesWritten) / outputBlockSize;
            var inputBlocksLeft = (maxBytesCanRead - bytesRead) / inputBlockSize;

            var blocksCanRead = Math.Min(outputBlocksLeft, inputBlocksLeft);
            blocksCanRead = Math.Min(payloadLeft / outputBlockSize, blocksCanRead);

            if ( blocksCanRead != 0 )
            {
                var bytesCanRead = blocksCanRead * inputBlockSize;
                var bytesDecrypted = Decryptor.TransformBlock(inArray, inStart + bytesRead, bytesCanRead, outArray, outStart + bytesWritten);

                bytesWritten += bytesDecrypted;
                PayloadWritten += bytesDecrypted;
                bytesRead += bytesCanRead;
            }
            else if ( payloadLeft != 0 && inputBlocksLeft > 0 )
            {
                var paddings = inputBlockSize - payloadLeft;

                if ( outputBlocksLeft > 0 )
                {
                    bytesWritten += Decryptor.TransformBlock(inArray, inStart + bytesRead, inputBlockSize, outArray, outStart + bytesWritten) - paddings;

                    HeaderRead = false;
                    PayloadWritten = 0;
                }
                else
                {
                    DecryptedCount = Decryptor.TransformBlock(inArray, inStart + bytesRead, inputBlockSize, DecryptedBytes, 0) - paddings;
                }

                bytesRead += inputBlockSize;
            }

            return bytesWritten > 0 || DecryptedCount > 0;
        }

        private bool TryReadHeader()
        {
            var limit = DecryptedCount;

            int i;
            for ( i = 1; i < limit; i++ )
            {
                if ( DecryptedBytes[i] < DecryptedBytes[i - 1] )
                {
                    i++;
                    break;
                }
            }

            if ( limit - i < 12 )
            {
                return false;
            }

            PairId = ReadInt(DecryptedBytes, ref i);
            SerialId = ReadInt(DecryptedBytes, ref i);
            PayloadSize = ReadInt(DecryptedBytes, ref i);

            var remainingDecrypted = limit - i;
            if ( remainingDecrypted > 0 )
            {
                Buffer.BlockCopy(DecryptedBytes, i, DecryptedBytes, 0, remainingDecrypted);
            }
            DecryptedCount = remainingDecrypted;

            return true;
        }

        // internal for testing
        internal static int ReadInt( byte[] array, ref int offset )
        {
            var result = 0;

            result |= array[offset++];
            result |= array[offset++] << 8;
            result |= array[offset++] << 16;
            result |= array[offset++] << 24;

            return result;
        }
    }
}
