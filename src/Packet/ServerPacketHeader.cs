using System.Runtime.InteropServices;

namespace ByteTransfer
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ServerPacketHeader
    {
        [FieldOffset(0)]
        public int Size;

        [FieldOffset(4)]
        public int PacketId;
    }
}