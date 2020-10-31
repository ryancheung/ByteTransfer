using System.Runtime.InteropServices;

namespace ByteTransfer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClientPacketHeader
    {
        public ushort Size;

        public int PacketId;

        public byte Compressed;
    }
}