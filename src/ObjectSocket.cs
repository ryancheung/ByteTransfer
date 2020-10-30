using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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

        protected AuthCrypt _authCrypt = new AuthCrypt();
        public BaseSession Session { get; protected set; }

        public const int HeaderSizeClient = 6;
        public const int HeaderSizeServer = 8;

        public int RecvHeaderSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ServerSocket ? HeaderSizeClient : HeaderSizeServer; }
        }

        public int SendHeaderSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ServerSocket ? HeaderSizeServer : HeaderSizeClient; }
        }

        protected override void ReadHandler()
        {
            MessageBuffer packet = GetReadBuffer();

            while (packet.GetActiveSize() > 0)
            {
                int size = RecvHeaderSize;

                if (packet.GetActiveSize() < size)
                    break;

                // We just received nice new header
                _authCrypt.DecryptRecv(packet.Data(), packet.Rpos(), size);
                var addr = Marshal.UnsafeAddrOfPinnedArrayElement(packet.Data(), packet.Rpos());

                int packetId;

                if (ServerSocket)
                {
                    var header = Marshal.PtrToStructure<ClientPacketHeader>(addr);
                    packet.ReadCompleted(size);
                    size = header.Size - 4;
                    packetId = header.PacketId;
                }
                else
                {
                    var header = Marshal.PtrToStructure<ServerPacketHeader>(addr);
                    packet.ReadCompleted(size);
                    size = header.Size - 4;
                    packetId = header.PacketId;
                }

                if (packet.GetActiveSize() < size)
                    break;

                DeserializePacket(packetId, size, packet);
            }

            AsyncRead();
        }

        protected void DeserializePacket(int packetId, int size, MessageBuffer packetBuffer)
        {
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

            var memory = new ReadOnlyMemory<byte>(packetBuffer.Data(), packetBuffer.Rpos(), size);

            var obj = genericDeserializeMethod.Invoke(null, new object[] { memory, null, null });

            if (Session != null)
                Session.ReceiveQueue.Enqueue(obj as ObjectPacket);

            packetBuffer.ReadCompleted(size);

            Console.WriteLine("{0} socket - received packet: {1}, length: {2}", ServerSocket ? "Server" : "Client", obj, size);
        }

        public void SendObjectPacket<T>(T packet) where T : ObjectPacket
        {
            if (!IsOpen()) return;

            var data = MessagePackSerializer.Serialize(packet);
            var size = data.Length + 4;

            var buffer = new ByteBuffer(SendHeaderSize + data.Length);
            if (ServerSocket)
                buffer.Append((int)size);
            else
                buffer.Append((ushort)size);
            buffer.Append(packet.PacketId);
            buffer.Append(data);

            _authCrypt.EncryptSend(buffer.Data(), 0, SendHeaderSize);

            SendPacket(buffer);
        }
    }
}
