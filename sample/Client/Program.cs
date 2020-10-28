using System;
using System.Threading;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Connecting to 127.0.0.1:3790");

            var client = new ByteTransfer.Client<AuthSocket>("127.0.0.1", 3790);

            Thread.Sleep(2000);
            client.Socket.CloseSocket();
        }
    }
}
