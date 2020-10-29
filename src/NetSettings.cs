using System.Reflection;
using NLog;

namespace ByteTransfer
{
    public static class NetSettings
    {
        public static Logger Logger { get; private set; }

        public static void SetupPackets(params Assembly[] packetAssemblies)
        {
            ObjectPacket.Initialize(packetAssemblies);
        }
    }
}