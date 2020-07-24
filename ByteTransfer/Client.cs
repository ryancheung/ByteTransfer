using System;
using System.Net.Sockets;

namespace ByteTransfer
{
    public class Client<T> where T : BaseSocket, new()
    {
        private string _host;
        private int _port;

        private TcpClient _tcpClient;
        public TcpClient TcpClient { get { return _tcpClient; } }

        private AsyncCallback _requestCallback;

        private T _socket;
        public T Socket { get { return _socket; } }

        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Client(string host, int port)
        {
            _tcpClient = new TcpClient();

            _host = host;
            _port = port;
            _requestCallback = Connecting;

            Start();
        }

        private void Connecting(IAsyncResult result)
        {
            if (Stopped)
            {
                _tcpClient.Close();
                return;
            }

            if (_tcpClient.Connected)
            {
                _tcpClient.EndConnect(result);

                _socket = new T();
                _socket.Create(_tcpClient.Client);

                _socket.Start();
            }
            else
                Stop();
        }

        public void Start()
        {
            if (Started) return;

            Started = true;

            _tcpClient.BeginConnect(_host, _port, _requestCallback, null);
        }

        public void Stop()
        {
            if (Stopped) return;

            Stopped = true;
            Started = false;

            if (_socket != null)
                _socket.DelayedCloseSocket();

            _socket = null;
        }
    }
}
