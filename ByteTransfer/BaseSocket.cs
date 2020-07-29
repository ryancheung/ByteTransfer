using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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
        private bool _closing;

        private bool _isWritingAsync;

        private bool _disposed = false;
        public bool Disposed { get { return _disposed; } }

        private readonly AsyncCallback ReceiveDataCallback;
        private readonly AsyncCallback SendDataCallback;
        private SocketError _error;

        public IPAddress RemoteAddress { get { return _remoteAddress; } }
        public int RemotePort { get { return _remotePort; } }

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
                _closed.Value = true;
                _socket.Close();
            }

            _disposed = true;
        }

        public virtual bool Update()
        {
            if (_closed.Value)
                return false;

            return true;
        }

        public bool IsOpen()
        {
            return !_closed.Value && !_closing;
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

        public void DelayedCloseSocket() { _closing = true; }

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
                _closing = true;
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
                else if (_closing)
                    CloseSocket();
            }
            catch (Exception)
            {
                CloseSocket();
            }
        }

        protected void AsyncProcessQueue()
        {
            if (_isWritingAsync)
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
    }
}
