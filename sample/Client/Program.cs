using System;
using System.Threading;
using Shared;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            ByteTransfer.NetSettings.Register(MessagePack.Resolvers.GeneratedResolver.Instance);
            ByteTransfer.NetSettings.InstallPackets(typeof(Shared.ClientPackets.Login).Assembly);

            Console.WriteLine("Connecting to 127.0.0.1:3790");

            var client = new ByteTransfer.NetClient<ClientSocket>("127.0.0.1", 3790);
            Thread.Sleep(100);

            while (true)
            {
                client.Socket.Session.Process();
            }
        }
    }
}
