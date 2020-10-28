using System;
using System.Collections.Generic;
using ByteTransfer;

namespace Server
{
    public class AuthSession : BaseSocket
    {
        public void SendPacket(ByteBuffer packet)
        {
            if (!IsOpen()) return;

            if (!packet.Empty)
            {
                var buffer = new MessageBuffer(packet.Wpos());
                buffer.Write(packet.Data(), packet.Wpos());
                QueuePacket(buffer);
            }
        }

        public override void Start()
        {
            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connection from {0}:{1} accepted", ipAddress, RemotePort);

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

                if (cmd == CMD_C_LOGON)
                {
                    Console.WriteLine("client cmd: {0}", cmd);
                    packet.ReadCompleted(sizeof(uint));

                    var data = BitConverter.ToInt32(packet.Data(), packet.Rpos());
                    Console.WriteLine("client data: {0}", data);
                    packet.ReadCompleted(sizeof(int));

                    var buffer = new ByteBuffer(12);
                    buffer.Append((ushort)10);
                    buffer.Append(CMD_S_LOGON);
                    buffer.Append(2048);
                    SendPacket(buffer);
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

