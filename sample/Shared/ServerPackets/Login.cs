using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace Shared.ServerPackets
{
    [MessagePackObject]
    public sealed class Login : ByteTransfer.ObjectPacket
    {
        [Key(0)]
        public string Result { get; set; }
    }
}
