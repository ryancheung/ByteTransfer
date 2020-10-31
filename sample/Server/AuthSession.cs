using System;
using ByteTransfer;

namespace Server
{
    public class AuthSession : ByteTransfer.BaseSession
    {
        public AuthSession(ObjectSocket socket) : base(socket) { }

        public void Process(Shared.ClientPackets.Login login)
        {
            Console.WriteLine("[AuthSession] Received login package, username: {0}, password: {1}", login.Username, login.Password);

            Socket.SendObjectPacket(new Shared.ServerPackets.Login { Result = login.Username }, true);
        }
    }
}