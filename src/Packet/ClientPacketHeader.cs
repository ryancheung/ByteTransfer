using System.Runtime.InteropServices;

namespace ByteTransfer
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ClientPacketHeader
    {
        [FieldOffset(0)]
        public ushort Size;

        [FieldOffset(2)]
        public int PacketId;
    }
}