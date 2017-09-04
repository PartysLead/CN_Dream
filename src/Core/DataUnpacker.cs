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
        readonly ArraySegment<byte> TempEncrypted, TempDecrypted;
        int EnTempWritten, DeTempWritten;
        int PairId, SerialId, PayloadSize, PayloadWritten;
        bool HeaderRead;

        public DataUnpacker( ICryptoTransform decryptor )
        {
            Decryptor = decryptor;
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

            var enArray = TempEncrypted.Array;
            var enStart = TempEncrypted.Offset;
            var maxBytesCanTemp = TempEncrypted.Count;

            var deArray = TempDecrypted.Array;
            var deStart = TempDecrypted.Offset;

            var inputBlockSize = Decryptor.InputBlockSize;
            var outputBlockSize = Decryptor.OutputBlockSize;

            bytesWritten = 0;
            bytesRead = 0;

            if ( PayloadWritten == 0 )
            {
                if ( !HeaderRead && DeTempWritten > 0 )
                {
                    HeaderRead = TryReadHeader();
                }

                while ( !HeaderRead && bytesRead < maxBytesCanRead )
                {
                    Debug.Assert(EnTempWritten == 0 || EnTempWritten % inputBlockSize != 0);

                    var minNeededBytes = inputBlockSize - EnTempWritten;
                    Debug.Assert(minNeededBytes > 0);

                    var bytesCanRead = Math.Min(minNeededBytes, maxBytesCanRead - bytesRead);
                    Buffer.BlockCopy(inArray, inStart + bytesRead, enArray, enStart + EnTempWritten, bytesCanRead);

                    bytesRead += bytesCanRead;
                    EnTempWritten += bytesCanRead;

                    if ( EnTempWritten % inputBlockSize == 0 )
                    {
                        DeTempWritten += Decryptor.TransformBlock(enArray, enStart + EnTempWritten, inputBlockSize, deArray, deStart + DeTempWritten);
                        EnTempWritten = 0;

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

            if ( DeTempWritten > 0 )
            {
                Debug.Assert(bytesWritten == 0);

                bytesWritten = Math.Min(payloadSize - PayloadWritten, Math.Min(DeTempWritten, maxBytesCanWrite));
                PayloadWritten += bytesWritten;

                Buffer.BlockCopy(deArray, deStart, outArray, outStart, bytesWritten);

                var newDeTempSize = DeTempWritten - bytesWritten;
                if ( newDeTempSize > 0 )
                {
                    Buffer.BlockCopy(deArray, deStart + bytesWritten, deArray, 0, newDeTempSize);
                }

                DeTempWritten = newDeTempSize;

                if ( bytesWritten < DeTempWritten )
                {
                    if ( PayloadWritten == payloadSize )
                    {
                        HeaderRead = false;
                        PayloadWritten = 0;
                    }

                    return true;
                }
            }

            Debug.Assert(DeTempWritten == 0);

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
                    DeTempWritten = Decryptor.TransformBlock(inArray, inStart + bytesRead, inputBlockSize, deArray, deStart) - paddings;
                }

                bytesRead += inputBlockSize;
            }

            return bytesWritten > 0 || DeTempWritten > 0;
        }

        private bool TryReadHeader()
        {
            var deArray = TempDecrypted.Array;
            var deStart = TempDecrypted.Offset;
            var limit = deStart + DeTempWritten;

            int i;
            for ( i = deStart + 1; i < limit; i++ )
            {
                if ( deArray[i] < deArray[i - 1] )
                {
                    i++;
                    break;
                }
            }

            if ( limit - i < 12 )
            {
                return false;
            }

            PairId = ReadInt(ref i);
            SerialId = ReadInt(ref i);
            PayloadSize = ReadInt(ref i);

            var remainingDecrypted = limit - i;
            if ( remainingDecrypted > 0 )
            {
                Buffer.BlockCopy(deArray, i, deArray, 0, remainingDecrypted);
            }
            DeTempWritten = remainingDecrypted;

            return true;
        }

        private int ReadInt( ref int offset )
        {
            var array = TempDecrypted.Array;
            var result = 0;

            result |= array[offset++];
            result |= array[offset++] << 8;
            result |= array[offset++] << 16;
            result |= array[offset++] << 24;

            return result;
        }
    }
}
