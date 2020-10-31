using System;
using System.Reflection;
using System.Collections.Generic;
using MessagePack;

namespace ByteTransfer
{
    public abstract class ObjectPacket
    {
        protected static readonly List<Type> Packets = new List<Type>();

        public static bool Initialized { get; private set; }

        internal static void Initialize(params Assembly[] packetAssemblies)
        {
            if (Initialized)
                return;

            Array.Resize(ref packetAssemblies, packetAssemblies.Length + 1);
            packetAssemblies[packetAssemblies.Length - 1] = typeof(ObjectPacket).GetTypeInfo().Assembly;

            foreach (var assembly in packetAssemblies)
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.BaseType != typeof(ObjectPacket))
                        continue;

                    if (Packets.IndexOf(type) > -1)
                        continue;

                    Packets.Add(type);
                }
            }

            Packets.Sort((x1, x2) =>
            {
                return string.Compare(x1.FullName, x2.FullName, StringComparison.Ordinal);
            });

            Initialized = true;
        }

        [IgnoreMember]
        public int PacketId { get { return Packets.IndexOf(GetType()); } }

        public static Type GetPacketType(int packetId)
        {
            if (packetId < 0 || packetId >= Packets.Count)
                return null;

            return Packets[packetId];
        }
    }
}
