using System;
using System.Threading;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ByteTransfer.Server<AuthSession>("127.0.0.1", 3790);

            server.StartNetwork();

            while(true)
            {
                Thread.Sleep(1);
            }
        }
    }
}
