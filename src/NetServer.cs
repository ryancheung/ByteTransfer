using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace ByteTransfer
{
    public class NetServer<T> where T : BaseSocket, new()
    {
        public bool NetworkStarted { get; protected set; }

        private TcpListener _listener;

        protected List<T> _sockets = new List<T>();

        private object _socketsLock = new object();

        private Thread _updateThread;

        public NetServer(string ip, ushort port)
        {
            _listener = new TcpListener(IPAddress.Parse(ip), port);
        }

        public virtual void StartNetwork(bool log = true)
        {
            if (NetworkStarted)
                return;

            _listener.Start();
            _listener.BeginAcceptSocket(AcceptedCallback, null);

            NetworkStarted = true;

            _updateThread = new Thread(new ThreadStart(Update)) { IsBackground = true, Name = "NetServerUpdate" };
            _updateThread.Start();

            if (log)
            {
                if (NetSettings.Logger != null)
                    NetSettings.Logger.Info("Network Started.");
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

            _updateThread.Join();
            _updateThread = null;

            foreach (var s in _sockets)
                s.Dispose();

            _sockets.Clear();

            if (log)
            {
                if (NetSettings.Logger != null)
                    NetSettings.Logger.Info("NetServer Stopped.");
                else
                    Console.WriteLine("NetServer Stopped.");
            }
        }

        private void Update()
        {
            while (NetworkStarted)
            {
                _sockets.RemoveAll(s =>
                {

                    if (!s.Update())
                    {
                        if (s.IsOpen())
                            s.CloseSocket();

                        OnSocketRemoved(s);

                        s.Dispose();

                        return true;
                    }

                    return false;
                });

                Thread.Sleep(10);
            }
        }

        protected virtual void OnSocketRemoved(T socket)
        {

        }

        protected virtual void OnSocketAdded(T socket)
        {

        }

        protected virtual void AddSocket(T sock)
        {
            lock (_socketsLock)
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
                if (NetSettings.Logger != null)
                    NetSettings.Logger.Warn(ex);
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
