using System;
using System.Net.Sockets;

namespace ByteTransfer
{
    public class NetClient<T> where T : BaseSocket, new()
    {
        private readonly string _host;
        private readonly int _port;

        private Socket _socketInternal;

        private T _socket;
        public T Socket { get { return _socket; } }

        private bool _connectFailed;
        public bool ConnectFailed { get { return _connectFailed;  } }

        private readonly AddressFamily _addressFamily;

        public bool IsOpen()
        {
            if (_socket == null || _socket.Disposed) return false;

            return _socket.IsOpen();
        }

        public NetClient(string host, int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            _host = host;
            _port = port;
            _addressFamily = addressFamily;

            Start();
        }

        private void ConnectCallback(IAsyncResult result)
        {
            if (_socketInternal == null || !_socketInternal.Connected)
            {
                _connectFailed = true;
                return;
            }

            _socketInternal.EndConnect(result);

            _socket = new T();
            _socket.Create(_socketInternal);

            _socket.Start();
        }

        public void Start()
        {
            if (_socket != null) return;

            _socketInternal = new Socket(_addressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socketInternal.BeginConnect(_host, _port, ConnectCallback, null);
        }

        public void Stop()
        {
            if (_socket != null)
            {
                _socket.CloseSocket();
                _socket.Dispose();
                return;
            }

            if (_socketInternal != null)
            {
                _socketInternal.Dispose();
                _socketInternal = null;
            }
        }
    }
}
