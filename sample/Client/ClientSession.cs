using System;
using ByteTransfer;

namespace Client
{
    public class ClientSession : ByteTransfer.BaseSession
    {
        public ClientSession(ObjectSocket socket) : base(socket) { }

        public void Process(Shared.ServerPackets.Login login)
        {
            Console.WriteLine("[ClientSession] Receveid login package, result: {0}", login.Result);

            Socket.SendObjectPacket(new Shared.ClientPackets.Login { Username = new Random().Next(0, 100000).ToString(), Password = "PWD" });
        }
    }
}
