using System;
using MessagePack;

namespace Shared.ClientPackets
{
    [MessagePackObject]
    public sealed class Login : ByteTransfer.ObjectPacket
    {
        [Key(0)]
        public string Username { get; set; }
        [Key(1)]
        public string Password { get; set; }
    }
}
