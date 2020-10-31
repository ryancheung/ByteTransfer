using System.Runtime.InteropServices;

namespace ByteTransfer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ServerPacketHeader
    {
        public int Size;

        public int PacketId;

        public byte Compressed;
    }
}