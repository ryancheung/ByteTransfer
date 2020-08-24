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

        private AddressFamily _addressFamily;

        public bool IsConnected
        {
            get
            {
                if (_tcpClient == null) return false;

                try
                {
                    return _tcpClient.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Client(string host, int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            _host = host;
            _port = port;
            _addressFamily = addressFamily;

            _requestCallback = Connecting;

            _updateTimer = new Timer(new TimerCallback(Update), null, 100, 100);

            Start();
        }

        private bool IsSocketConnected()
        {
            if (_socket == null || _socket.Disposed) return false;

            try
            {
                return !(_socket.Socket.Poll(1, SelectMode.SelectRead) && _socket.Socket.Available == 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Update(object state)
        {
            if (_stopped) return;

            if (_socket == null || _socket.Disposed) return;

            if (!IsSocketConnected())
            {
                Stop();
            }
        }

        private void Connecting(IAsyncResult result)
        {
            if (_stopped)
            {
                _tcpClient.Close();
                _tcpClient = null;
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

            _tcpClient = new TcpClient(_addressFamily);

            _started = true;
            _stopped = false;

            _tcpClient.BeginConnect(_host, _port, _requestCallback, null);
        }

        public void Stop()
        {
            if (_stopped) return;

            _stopped = true;
            _started = false;

            if (_socket != null && !_socket.Disposed)
                _socket.Dispose();
            _socket = null;
            _tcpClient = null;
        }
    }
}
