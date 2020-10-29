using System;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using MessagePack;
using NLog;

namespace ByteTransfer
{
    public abstract class ObjectSocket : BaseSocket
    {
        public static readonly MethodInfo DeserializeMethod = typeof(MessagePackSerializer)
            .GetMethod("Deserialize", new[] { typeof(ReadOnlyMemory<byte>), typeof(MessagePackSerializerOptions), typeof(CancellationToken) });

        private static Dictionary<Type, MethodInfo> _GenericDeserializeMethods = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// Packet layout is determinined by this option.
        /// <para> Client packet buffer layout: Size(ushort) | PacketId(int) | Payload(byte[])</para>
        /// <para> Server packet buffer layout: Size(int) | PacketId(int) | Payload(byte[])</para>
        /// </summary>
        public bool ServerSocket { get; protected set; }

        public BaseSession Session { get; protected set; }

        protected override void ReadHandler()
        {
            MessageBuffer packetBuffer = GetReadBuffer();

            while (packetBuffer.GetActiveSize() > 0)
            {
                int size = ServerSocket ? sizeof(ushort) : sizeof(int);

                if (packetBuffer.GetActiveSize() < size)
                    break;

                if (ServerSocket)
                {
                    size = BitConverter.ToUInt16(packetBuffer.Data(), packetBuffer.Rpos());
                    packetBuffer.ReadCompleted(sizeof(ushort));
                }
                else
                {
                    size = BitConverter.ToInt32(packetBuffer.Data(), packetBuffer.Rpos());
                    packetBuffer.ReadCompleted(sizeof(int));
                }

                if (packetBuffer.GetActiveSize() < size)
                    break;

                DeserializePacket(size, packetBuffer);
            }

            AsyncRead();
        }

        protected void DeserializePacket(int size, MessageBuffer packetBuffer)
        {
            var packetId = BitConverter.ToInt32(packetBuffer.Data(), packetBuffer.Rpos());
            packetBuffer.ReadCompleted(sizeof(int));

            var packetType = ObjectPacket.GetPacketType(packetId);

            if (packetType == null)
            {
                var message = string.Format("Received invalid Packet of Id {0}!", packetId);

                if (NetSettings.Logger != null)
                    NetSettings.Logger.Info(message);
                else
                    Console.WriteLine(message);

                return;
            }

            // Invoke the following generic method here to enable AOT
            //   public static T Deserialize<T>(ReadOnlyMemory<byte> buffer, MessagePackSerializerOptions options = null, CancellationToken cancellationToken = default);
            MethodInfo genericDeserializeMethod;
            if (!_GenericDeserializeMethods.TryGetValue(packetType, out genericDeserializeMethod))
            {
                genericDeserializeMethod = DeserializeMethod.MakeGenericMethod(packetType);
                _GenericDeserializeMethods[packetType] = genericDeserializeMethod;
            }

            var packetLength = size - sizeof(int);
            var memory = new ReadOnlyMemory<byte>(packetBuffer.Data(), packetBuffer.Rpos(), packetLength);

            var obj = genericDeserializeMethod.Invoke(null, new object[] { memory, null, null });

            if (Session != null)
                Session.ReceiveQueue.Enqueue(obj as ObjectPacket);

            packetBuffer.ReadCompleted(packetLength);

            Console.WriteLine("{0} socket - received packet: {1}, length: {2}", ServerSocket ? "Server" : "Client", obj, packetLength);
        }
    }
}
