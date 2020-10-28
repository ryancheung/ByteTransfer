using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using NLog;

namespace ByteTransfer
{
    public class Server<T> where T : BaseSocket, new()
    {
        public bool NetworkStarted { get; protected set; }

        private TcpListener _listener;

        protected List<T> _sockets = new List<T>();

        private Logger _logger;

        private Timer _updateTimer;

        private object _socketsLock = new object();

        public Server(string ip, ushort port, Logger logger = null)
        {
            _listener = new TcpListener(IPAddress.Parse(ip), port);
            _logger = logger;
        }

        public virtual void StartNetwork(bool log = true)
        {
            if (NetworkStarted)
                return;

            _listener.Start();
            _listener.BeginAcceptSocket(AcceptedCallback, null);

            NetworkStarted = true;

            _updateTimer = new Timer(Update, null, 10, 10);

            if (log)
            {
                if (_logger != null)
                    _logger.Info("Network Started.");
                else
                    Console.WriteLine("Network Started.");
            }
        }

        public virtual void StopNetwork(bool log = true)
        {
            if (!NetworkStarted)
                return;

            NetworkStarted = false;

            _listener.Stop();

            _updateTimer.Dispose();
            _updateTimer = null;

            foreach(var s in _sockets)
                s.Dispose();

            _sockets.Clear();

            if (log)
            {
                if (_logger != null)
                    _logger.Info("Network Stopped.");
                else
                    Console.WriteLine("Network Stopped.");
            }
        }

        private void Update(object state)
        {
            if (!NetworkStarted)
                return;

            _sockets.RemoveAll(s => {

                if (!s.Update())
                {
                    if (s.IsOpen())
                        s.CloseSocket();

                    OnSocketRemoved(s);

                    return true;
                }

                return false;
            });
        }

        protected virtual void OnSocketRemoved(T socket)
        {

        }

        protected virtual void OnSocketAdded(T socket)
        {

        }

        protected virtual void AddSocket(T sock)
        {
            lock(_socketsLock)
            {
                _sockets.Add(sock);
                OnSocketAdded(sock);
            }
        }

        protected virtual void AcceptedCallback(IAsyncResult result)
        {
            T baseSocket = null;

            try
            {
                if (_listener == null || !_listener.Server.IsBound) return;

                var socket = _listener.EndAcceptSocket(result);

                baseSocket = new T();
                baseSocket.Create(socket);

                baseSocket.Start();
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.Warn(ex);
                else
                    Console.WriteLine(string.Format("Socket accept error: {0}", ex));
            }
            finally
            {
                if (baseSocket != null)
                    AddSocket(baseSocket);

                if (_listener != null && _listener.Server.IsBound)
                    _listener.BeginAcceptSocket(AcceptedCallback, null);
            }
        }
    }
}
