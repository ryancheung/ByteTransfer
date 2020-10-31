using System;
using ByteTransfer;

namespace Server
{
    public class AuthSocket : ObjectSocket
    {
        public override void Start()
        {
            ServerSocket = true;

            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connection from {0}:{1} accepted", ipAddress, RemotePort);

            Session = new AuthSession(this);
            World.AddSession(Session);

            _authCrypt.Init("key", Shared.Keys.ServerEncryptionKey, Shared.Keys.ClientEncryptionKey, ServerSocket);

            AsyncRead();
        }
    }
}
