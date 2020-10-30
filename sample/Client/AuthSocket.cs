using System;
using ByteTransfer;
using MessagePack;
using NLog;

namespace Client
{
    public class AuthSocket : ObjectSocket
    {
        public override void Start()
        {
            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

            _authCrypt.Init("key", Shared.Keys.ServerEncryptionKey, Shared.Keys.ClientEncryptionKey, ServerSocket);

            var loginPacket = new Shared.ClientPackets.Login { Username = "foo", Password = "bar" };
            var data = MessagePackSerializer.Serialize(loginPacket);

            var buffer = new ByteBuffer(2 + 4 + data.Length);
            buffer.Append((ushort)(data.Length + 4));
            buffer.Append(loginPacket.PacketId);
            buffer.Append(data);

            _authCrypt.EncryptSend(buffer.Data(), 0, 6);

            SendPacket(buffer);

            AsyncRead();
        }
    }
}
