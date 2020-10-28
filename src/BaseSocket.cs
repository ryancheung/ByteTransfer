using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ByteTransfer
{
    public abstract class BaseSocket : IDisposable
    {
        public const int READ_BLOCK_SIZE = 4096;

        private Socket _socket;
        private IPAddress _remoteAddress;
        private int _remotePort;

        public Socket Socket { get { return _socket; } }

        private MessageBuffer _readBuffer;
        private Queue<MessageBuffer> _writeQueue = new Queue<MessageBuffer>();

        private InterlockedBoolean _closed;
        private InterlockedBoolean _closing;
        private Timer _closingTimer;

        private bool _isWritingAsync;

        private bool _disposed = false;
        public bool Disposed { get { return _disposed; } }

        private readonly AsyncCallback ReceiveDataCallback;
        private readonly AsyncCallback SendDataCallback;
        private SocketError _error;

        public IPAddress RemoteAddress { get { return _remoteAddress; } }
        public int RemotePort { get { return _remotePort; } }

        public bool Shutdown { get; private set; }

        public BaseSocket()
        {
            _readBuffer = new MessageBuffer(READ_BLOCK_SIZE);

            ReceiveDataCallback = ReadHandlerInternal;
            SendDataCallback = WriteHandlerInternal;
        }

        public void Create(Socket socket)
        {
            _socket = socket;

            _remoteAddress = (socket.RemoteEndPoint as IPEndPoint).Address;
            _remotePort = (socket.RemoteEndPoint as IPEndPoint).Port;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                CloseSocket();
                _socket.Dispose();
            }

            _disposed = true;
        }

        private void SetClosing()
        {
            if (_closing.Exchange(true))
                return;

            _closingTimer = new Timer((o) =>
            {
                CloseSocket();
            }, null, 2000, Timeout.Infinite);
        }

        public bool IsOpen()
        {
            return !_closed.Value && !_closing.Value;
        }

        public virtual void Start() { }

        public void CloseSocket()
        {
            if (_closed.Exchange(true))
                return;

            if (_socket.Connected)
                _socket.Shutdown(SocketShutdown.Send);

            OnClose();
        }

        public MessageBuffer GetReadBuffer() { return _readBuffer; }

        protected void SetNoDelay(bool enable)
        {
            _socket.NoDelay = enable;
        }

        protected void SetBlocking(bool enable)
        {
            _socket.Blocking = enable;
        }

        protected virtual void OnClose() { }

        protected virtual void ReadHandler() { }

        private void ReadHandlerInternal(IAsyncResult result)
        {
            switch (_error)
            {
                case SocketError.Success:
                case SocketError.IOPending:
                    break;
                default:
                    CloseSocket();
                    return;
            }

            try
            {
                var transferredBytes = _socket.EndReceive(result);

                if (transferredBytes == 0) // Handle TCP Shutdown
                {
                    Shutdown = true;
                    return;
                }

                _readBuffer.WriteCompleted(transferredBytes);
                ReadHandler();
            }
            catch (Exception)
            {
                CloseSocket();
            }
        }

        public void AsyncRead()
        {
            if (!IsOpen())
                return;

            _readBuffer.Normalize();
            _readBuffer.EnsureFreeSpace();

            try
            {
                _socket.BeginReceive(_readBuffer.Data(), _readBuffer.Wpos(), _readBuffer.GetRemainingSpace(),
                    SocketFlags.None, out _error, ReceiveDataCallback, null);
            }
            catch (Exception)
            {
                SetClosing();
            }
        }

        private void WriteHandlerInternal(IAsyncResult result)
        {
            if (_error > 0)
            {
                CloseSocket();
                return;
            }

            try
            {
                var transferedBytes = _socket.EndSend(result);

                _isWritingAsync = false;
                _writeQueue.Peek().ReadCompleted(transferedBytes);

                if (_writeQueue.Peek().GetActiveSize() <= 0)
                    _writeQueue.Dequeue();

                if (_writeQueue.Count > 0)
                    AsyncProcessQueue();
                else if (_closing.Value)
                    CloseSocket();
            }
            catch (Exception)
            {
                CloseSocket();
            }
        }

        protected void AsyncProcessQueue()
        {
            if (_isWritingAsync || Shutdown || _closed.Value)
                return;

            _isWritingAsync = true;

            var buffer = _writeQueue.Peek();

            try
            {
                _socket.BeginSend(buffer.Data(), buffer.Rpos(), buffer.GetActiveSize(),
                    SocketFlags.None, out _error, SendDataCallback, null);
            }
            catch (Exception)
            {
                CloseSocket();
            }
        }

        public void QueuePacket(MessageBuffer buffer)
        {
            _writeQueue.Enqueue(buffer);

            AsyncProcessQueue();
        }

        public virtual bool Update()
        {
            if (_closed.Value)
                return false;

            return true;
        }
    }
}
