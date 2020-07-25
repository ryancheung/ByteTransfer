using System;
using System.Threading;
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

        private bool _started;
        public bool Started { get { return _started; } }
        private bool _stopped;
        public bool Stopped { get { return _stopped; } }

        private Timer _updateTimer;

        public Client(string host, int port)
        {
            _host = host;
            _port = port;
            _requestCallback = Connecting;

            _updateTimer = new Timer(new TimerCallback(Update), null, 10, 10);

            Start();
        }

        private void Update(object state)
        {
            if (_stopped) return;

            if (_socket == null || _socket.Disposed) return;

            if (!_socket.Update() || !_tcpClient.Connected || !_socket.Socket.Connected)
            {
                Stop();
            }
        }

        private void Connecting(IAsyncResult result)
        {
            if (_stopped)
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
            if (_started) return;

            _tcpClient = new TcpClient();

            _started = true;
            _stopped = false;

            _tcpClient.BeginConnect(_host, _port, _requestCallback, null);
        }

        public void Stop()
        {
            if (_stopped) return;

            _stopped = true;
            _started = false;

            if (!_socket.Disposed)
                _socket.Dispose();
            _socket = null;
        }
    }
}
