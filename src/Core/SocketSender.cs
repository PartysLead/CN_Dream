using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class SocketSender : ISocketSender
    {
        readonly IPool<SocketAsyncEventArgs> SendArgsPool;

        public SocketSender( IPool<SocketAsyncEventArgs> sendArgsPool )
        {
            SendArgsPool = sendArgsPool;
        }

        public Task SendDataAsync( Socket socket, byte[] array, int offset, int count )
        {
            var tcs = new TaskCompletionSource<bool>();

            var e = SendArgsPool.Acquire();
            e.SetBuffer(array, offset, count);
            e.UserToken = tcs;
            e.Completed += OnDataSent;

            SendData(socket, e);

            return tcs.Task;
        }

        private void SendData( Socket socket, SocketAsyncEventArgs e )
        {
            if ( !socket.SendAsync(e) )
            {
                OnDataSent(socket, e);
            }
        }

        private void OnDataSent( object sender, SocketAsyncEventArgs e )
        {
            var socket = (Socket)sender;
            var tcs = (TaskCompletionSource<bool>)e.UserToken;

            if ( e.SocketError == SocketError.Success )
            {
                var bytesSent = e.BytesTransferred;
                var bytesRemain = e.Count - bytesSent;
                if ( bytesRemain > 0 )
                {
                    e.SetBuffer(e.Offset + bytesSent, bytesRemain);
                    SendData(socket, e);
                }
                else
                {
                    ReleaseSendArgs(e);
                    tcs.SetResult(true);
                }
            }
            else
            {
                ReleaseSendArgs(e);
                tcs.SetException(new SocketException((int)e.SocketError));
            }
        }

        private void ReleaseSendArgs( SocketAsyncEventArgs e )
        {
            e.SetBuffer(null, 0, 0);
            e.UserToken = null;
            e.Completed -= OnDataSent;

            SendArgsPool.Release(e);
        }
    }
}
