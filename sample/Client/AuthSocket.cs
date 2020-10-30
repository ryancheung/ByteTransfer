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

            SendObjectPacket(loginPacket);

            AsyncRead();
        }
    }
}
