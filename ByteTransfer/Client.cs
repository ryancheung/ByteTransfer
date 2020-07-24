using System;
using System.Threading;
using System.Net.Sockets;

namespace ByteTransfer
{
    public class Client<T> : TcpClient where T : BaseSocket, new()
    {
        private Thread _workerThread;

        private string _host;
        private int _port;
        private AsyncCallback _requestCallback;

        private T _socket;
        public T Socket { get { return _socket; } }

        public Client(string host, int port)
        {
            _host = host;
            _port = port;
            _requestCallback = Connecting;

            _workerThread = new Thread(new ThreadStart(Run)) { IsBackground = true, Name = "NetworkThread" };
            _workerThread.Start();
        }

        public void Restart()
        {
            if (Connected)
                this.Close();

            _workerThread = new Thread(new ThreadStart(Run)) { IsBackground = true };
            _workerThread.Start();
        }

        private void Connecting(IAsyncResult result)
        {
            if (Connected)
            {
                EndConnect(result);

                _socket = new T();
                _socket.Create(Client);

                _socket.Start();
            }
        }

        private void Run()
        {
            BeginConnect(_host, _port, _requestCallback, null);
        }
    }
}