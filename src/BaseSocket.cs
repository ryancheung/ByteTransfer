using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        private ConcurrentQueue<MessageBuffer> _writeQueue = new ConcurrentQueue<MessageBuffer>();

        private InterlockedBoolean _closed;
        private bool _closing;

        private InterlockedBoolean _isWritingAsync;

        private bool _disposed = false;
        public bool Disposed { get { return _disposed; } }

        private readonly AsyncCallback ReceiveDataCallback;
        private readonly AsyncCallback SendDataCallback;
        private SocketError _error;

        public IPAddress RemoteAddress { get { return _remoteAddress; } }
        public int RemotePort { get { return _remotePort; } }

        public bool LogException { get; protected set; }

        public int TotalBytesSent { get; protected set; }
        public int TotalBytesReceived { get; protected set; }

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

        /// <summary>
        /// Marks the socket for closing after write buffer becomes empty
        /// </summary>
        public void DelayedCloseSocket()
        {
            _closing = true;
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
                if (!_socket.Connected)
                    return;

                var transferredBytes = _socket.EndReceive(result);
                TotalBytesReceived += transferredBytes;

                if (transferredBytes == 0) // Handle TCP Shutdown
                {
                    CloseSocket();
                    return;
                }

                _readBuffer.WriteCompleted(transferredBytes);
                ReadHandler();
            }
            catch (Exception ex)
            {
                CloseSocket();

                if (LogException)
                {
                    if (NetSettings.Logger != null)
                        NetSettings.Logger.Warn(ex);
                    else
                        Console.WriteLine(ex);
                }
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
            catch (Exception ex)
            {
                CloseSocket();

                if (LogException)
                {
                    if (NetSettings.Logger != null)
                        NetSettings.Logger.Warn(ex);
                    else
                        Console.WriteLine(ex);
                }
            }
        }

        private void WriteHandlerInternal(IAsyncResult result)
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
                var transferedBytes = _socket.EndSend(result);
                TotalBytesSent += transferedBytes;

                MessageBuffer buffer = null;
                if (_writeQueue.Count > 0)
                    while (!_writeQueue.TryPeek(out buffer)) { };

                buffer.ReadCompleted(transferedBytes);

                if (buffer.GetActiveSize() <= 0)
                    while (!_writeQueue.TryDequeue(out buffer)) { };

                _isWritingAsync.Exchange(false);

                if (_writeQueue.Count > 0)
                    AsyncProcessQueue();
                else if (_closing)
                    CloseSocket();
            }
            catch (Exception ex)
            {
                CloseSocket();

                if (LogException)
                {
                    if (NetSettings.Logger != null)
                        NetSettings.Logger.Warn(ex);
                    else
                        Console.WriteLine(ex);
                }
            }
        }

        protected void AsyncProcessQueue()
        {
            if (_closed.Value)
                return;

            if (_isWritingAsync.Exchange(true))
                return;

            MessageBuffer buffer = null;
            if (_writeQueue.Count > 0)
                while (!_writeQueue.TryPeek(out buffer)) { };

            try
            {
                _socket.BeginSend(buffer.Data(), buffer.Rpos(), buffer.GetActiveSize(),
                    SocketFlags.None, out _error, SendDataCallback, null);
            }
            catch (Exception ex)
            {
                CloseSocket();

                if (LogException)
                {
                    if (NetSettings.Logger != null)
                        NetSettings.Logger.Warn(ex);
                    else
                        Console.WriteLine(ex);
                }
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

        public void SendPacket(ByteBuffer packet)
        {
            if (!IsOpen()) return;

            if (!packet.Empty && packet.Wpos() > 0)
            {
                var buffer = new MessageBuffer(packet.Wpos());
                buffer.Write(packet.Data(), packet.Wpos());
                QueuePacket(buffer);
            }
        }

        public void SendPacket(MessageBuffer packet)
        {
            if (!IsOpen()) return;

            if (packet.Wpos() > 0)
                QueuePacket(packet);
        }
    }
}
