using System;
using ByteTransfer;
using MessagePack;
using NLog;

namespace Client
{
    public class ClientSocket : ObjectSocket
    {
        public override void Start()
        {
            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

            Session = new ClientSession(this);

            _authCrypt.Init("key", Shared.Keys.ServerEncryptionKey, Shared.Keys.ClientEncryptionKey, ServerSocket);

            var loginPacket = new Shared.ClientPackets.Login { Username = "foo", Password = "bar" };

            SendPacket(loginPacket);

            AsyncRead();
        }
    }
}
