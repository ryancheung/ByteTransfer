using System;
using ByteTransfer;

namespace Client
{
    public class ClientSession : ByteTransfer.BaseSession
    {
        public ClientSession(ObjectSocket socket) : base(socket) { }

        public void Process(Shared.ServerPackets.Login login)
        {
            Console.WriteLine("[ClientSession] Received login package, result: {0}", login.Result);

            Socket.SendObjectPacket(new Shared.ClientPackets.Login { Username = new Random().Next(9999999, 999999999).ToString(), Password = "PWD" }, true);
        }
    }
}
