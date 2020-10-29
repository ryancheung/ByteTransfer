using System;
using System.Collections.Generic;
using ByteTransfer;

namespace Client
{
    public class AuthSocket : BaseSocket
    {
        public override void Start()
        {
            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

            var buffer = new ByteBuffer(10);
            buffer.Append((ushort)10);
            buffer.Append(CMD_C_LOGON);
            buffer.Append(1024);
            SendPacket(buffer);

            AsyncRead();
        }

        public const uint CMD_C_LOGON = 0;
        public const uint CMD_S_LOGON = 1;

        protected override void ReadHandler()
        {
            MessageBuffer packet = GetReadBuffer();

            while (packet.GetActiveSize() > 0)
            {
                if (packet.GetActiveSize() < 2)
                    break;

                var size = BitConverter.ToUInt16(packet.Data(), packet.Rpos());
                packet.ReadCompleted(sizeof(ushort));

                if (packet.GetActiveSize() < size - 2)
                    break;

                var cmd = BitConverter.ToUInt32(packet.Data(), packet.Rpos());

                if (cmd == CMD_S_LOGON)
                {
                    Console.WriteLine("server cmd: {0}", cmd);
                    packet.ReadCompleted(sizeof(uint));

                    var data = BitConverter.ToInt32(packet.Data(), packet.Rpos());
                    Console.WriteLine("server data: {0}", data);
                    packet.ReadCompleted(sizeof(int));
                }
            }

            AsyncRead();
        }

        private bool HandleLogonResult(ushort result, int code)
        {
            return true;
        }
    }
}
