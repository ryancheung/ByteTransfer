using System;

namespace Server
{
    public class AuthSession : ByteTransfer.BaseSession
    {
        public void Process(Shared.ClientPackets.Login login)
        {
            Console.WriteLine("Receveid login package, username: {0}, password: {1}", login.Username, login.Password);
        }
    }
}