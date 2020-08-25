using System;
using System.Net.Sockets;

namespace ByteTransfer
{
    public class Client<T> where T : BaseSocket, new()
    {
        private readonly string _host;
        private readonly int _port;

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

        public Client(string host, int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            _host = host;
            _port = port;
            _addressFamily = addressFamily;

            Start();
        }

        private void ConnectCallback(IAsyncResult result)
        {
            Socket client = (Socket)result.AsyncState;

            if (!client.Connected)
            {
                _connectFailed = true;
                return;
            }

            client.EndConnect(result);

            _socket = new T();
            _socket.Create(client);

            _socket.Start();
        }

        public void Start()
        {
            if (_socket != null) return;

            Socket client = new Socket(_addressFamily, SocketType.Stream, ProtocolType.Tcp);
            client.BeginConnect(_host, _port, ConnectCallback, client);
        }
    }
}
