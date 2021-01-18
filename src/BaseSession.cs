using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Concurrent;
using NLog;

namespace ByteTransfer
{
    public abstract class BaseSession
    {
        public static readonly Dictionary<Type, MethodInfo> PacketMethods = new Dictionary<Type, MethodInfo>();

        protected ConcurrentQueue<ObjectPacket> _receiveQueue = new ConcurrentQueue<ObjectPacket>();

        public ObjectSocket Socket { get; protected set; }

        protected abstract TimeSpan TimeOutDelay { get; }
        public DateTime TimeOutTime { get; set; }

        private bool _disconnecting;
        public bool Disconnecting
        {
            get { return _disconnecting; }
            set
            {
                if (_disconnecting == value) return;
                _disconnecting = value;
                TimeOutTime = DateTime.UtcNow.AddSeconds(2);
            }
        }


        private static object[] _parameterCache = new object[1] { null };

        public BaseSession(ObjectSocket socket)
        {
            Socket = socket;
            UpdateTimeOut();
        }

        public virtual void SendPacket(ObjectPacket packet, bool compress = false)
        {
            if (Socket != null)
                Socket.SendPacket(packet, compress);
        }

        public void QueuePacket(ObjectPacket packet)
        {
            _receiveQueue.Enqueue(packet);
        }

        public void UpdateTimeOut()
        {
            if (Disconnecting) return;

            TimeOutTime = DateTime.UtcNow + TimeOutDelay;
        }

        protected virtual void OnTimeout() {}

        /// <summary>
        /// Process received packets.
        /// </summary>
        public virtual void Process()
        {
            while (!_receiveQueue.IsEmpty)
            {
                try
                {
                    ObjectPacket p;
                    if (!_receiveQueue.TryDequeue(out p))
                        continue;

                    ProcessPacket(p);
                }
                catch (NotImplementedException ex)
                {
                    if (NetSettings.Logger != null)
                        NetSettings.Logger.Warn(ex);
                    else
                        Console.WriteLine(ex);
                }
            }

            if (DateTime.UtcNow >= TimeOutTime)
            {
                OnTimeout();
                return;
            }

            if (!Disconnecting)
                UpdateTimeOut();
        }

        protected virtual void OnBeforeProcessPacket() { }
        protected virtual void OnAfterProcessPacket() { }

        protected void ProcessPacket(ObjectPacket p)
        {
            if (p == null) return;

            OnBeforeProcessPacket();

            var packetType = ObjectPacket.GetPacketType(p.PacketId);

            MethodInfo processMethod;
            if (!PacketMethods.TryGetValue(packetType, out processMethod))
            {
                processMethod = GetType().GetMethod("Process", new[] { packetType });
                PacketMethods[packetType] = processMethod;
            }

            if (processMethod == null)
                throw new NotImplementedException(string.Format("Not Implemented Exception: Method Process({0}).", packetType));

            _parameterCache[0] = p;

            processMethod.Invoke(this, _parameterCache);

            OnAfterProcessPacket();
        }
    }
}
