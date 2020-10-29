using System;
using System.Threading;
using Shared;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            MessagePackManager.Register();
            ByteTransfer.NetSettings.SetupPackets(typeof(Shared.ClientPackets.Login).Assembly);

            var server = new ByteTransfer.NetServer<AuthSocket>("127.0.0.1", 3790);

            server.StartNetwork();

            while(true)
            {
                World.Process();
            }
        }
    }
}
