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
        public static readonly MethodInfo DeserializeMethod;
        public static readonly MethodInfo SerializeMethod;

        private static Dictionary<Type, MethodInfo> _GenericDeserializeMethods = new Dictionary<Type, MethodInfo>();
        private static Dictionary<Type, MethodInfo> _GenericSerializeMethods = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// Packet layout is determinined by this option.
        /// <para> Client packet buffer layout: Size(ushort) | PacketId(int) | Compressed(bool) | Payload(byte[])</para>
        /// <para> Server packet buffer layout: Size(int) | PacketId(int) | Compressed(bool) | Payload(byte[])</para>
        /// </summary>
        public bool ServerSocket { get; protected set; }

        protected AuthCrypt _authCrypt = new AuthCrypt();
        public BaseSession Session { get; protected set; }

        public const int HeaderSizeClient = 7; // ushort+int+bool
        public const int HeaderSizeServer = 9; // int+int+bool
        public const int HeaderTailSize = 5; // int+bool

        private static ThreadLocal<object[]> _parameterCache = new ThreadLocal<object[]>(() => {
            return new object[3] { null, null, null };
        });

        private static ThreadLocal<object[]> _parameterCache2 = new ThreadLocal<object[]>(() => {
            return new object[3] { null, null, null };
        });

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

        static ObjectSocket()
        {
            var type = typeof(MessagePackSerializer);
            DeserializeMethod = type.GetMethod("Deserialize", new[] { typeof(ReadOnlyMemory<byte>), typeof(MessagePackSerializerOptions), typeof(CancellationToken) });

            foreach(var method in type.GetMethods())
            {
                if (method.ToString() == "Byte[] Serialize[T](T, MessagePack.MessagePackSerializerOptions, System.Threading.CancellationToken)")
                {
                    SerializeMethod = method;
                    break;
                }
            }

            if (SerializeMethod == null)
                throw new ApplicationException("MessagePack serialize method search failed!");
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
                bool compressed;

                if (ServerSocket)
                {
                    var header = Marshal.PtrToStructure<ClientPacketHeader>(addr);
                    size = header.Size + sizeof(ushort);
                    packetId = header.PacketId;
                    compressed = header.Compressed != 0;
                }
                else
                {
                    var header = Marshal.PtrToStructure<ServerPacketHeader>(addr);
                    size = header.Size + sizeof(int);
                    packetId = header.PacketId;
                    compressed = header.Compressed != 0;
                }

                if (packet.GetActiveSize() < size)
                    break;

                packet.ReadCompleted(RecvHeaderSize);
                size -= RecvHeaderSize;
                DeserializePacket(packetId, compressed, size, packet);
            }

            AsyncRead();
        }

        protected void DeserializePacket(int packetId, bool compressed, int size, MessageBuffer packetBuffer)
        {
            var packetType = ObjectPacket.GetPacketType(packetId);

            if (packetType == null)
            {
                var message = string.Format("Received invalid Packet of Id {0}!", packetId);

                if (NetSettings.Logger != null)
                    NetSettings.Logger.Warn(message);
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
            _parameterCache.Value[0] = memory;
            _parameterCache.Value[1] = compressed ? NetSettings.LZ4CompressOptions : NetSettings.MessagePackOptions;

            var obj = genericDeserializeMethod.Invoke(null, _parameterCache.Value);

            if (Session != null)
                Session.QueuePacket(obj as ObjectPacket);

            packetBuffer.ReadCompleted(size);
        }

        public void SendPacket(ObjectPacket packet, bool compress = false)
        {
            if (!IsOpen()) return;

            // Invoke the following generic method here to enable AOT
            //   public static byte[] Serialize<T>(T value, MessagePackSerializerOptions options = null, CancellationToken cancellationToken = default);

            MethodInfo genericSerializeMethod;
            var packetType = packet.GetType();
            if (!_GenericSerializeMethods.TryGetValue(packetType, out genericSerializeMethod))
            {
                genericSerializeMethod = SerializeMethod.MakeGenericMethod(packetType);
                _GenericSerializeMethods[packetType] = genericSerializeMethod;
            }

            _parameterCache2.Value[0] = packet;
            _parameterCache2.Value[1] = compress ? NetSettings.LZ4CompressOptions : NetSettings.MessagePackOptions;

            var data = genericSerializeMethod.Invoke(null, _parameterCache2.Value) as byte[];

            var size = data.Length + HeaderTailSize;

            var buffer = new MessageBuffer(size + (ServerSocket ? 4 : 2));

            if (ServerSocket)
                buffer.Write(BitConverter.GetBytes((int)size), 4);
            else
                buffer.Write(BitConverter.GetBytes((ushort)size), 2);

            buffer.Write(BitConverter.GetBytes(packet.PacketId), 4);
            buffer.Write(BitConverter.GetBytes(compress), 1);
            buffer.Write(data, data.Length);

            _authCrypt.EncryptSend(buffer.Data(), 0, SendHeaderSize);

            base.SendPacket(buffer);
        }
    }
}
