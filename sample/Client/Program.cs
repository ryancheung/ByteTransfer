using System;
using System.Threading;
using Shared;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            MessagePackManager.Register();
            ByteTransfer.NetSettings.SetupPackets(typeof(Shared.ClientPackets.Login).Assembly);

            Console.WriteLine("Connecting to 127.0.0.1:3790");

            var client = new ByteTransfer.NetClient<AuthSocket>("127.0.0.1", 3790);

            while(true)
            {
                //client.Socket.Process();
                Thread.Sleep(1000);
            }
        }
    }
}
