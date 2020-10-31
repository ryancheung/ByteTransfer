using System;
using System.Reflection;
using NLog;
using MessagePack;
using MessagePack.Resolvers;

namespace ByteTransfer
{
    public static class NetSettings
    {
        private static bool _serializerRegistered;

        public static Logger Logger { get; private set; }

        public static MessagePackSerializerOptions LZ4CompressOptions { get; private set; }

        /// <summary>
        /// Collect all ObjectPacket classes in assemblies for handling packet serialization and deserialization.
        /// </summary>
        public static void InstallPackets(params Assembly[] packetAssemblies)
        {
            ObjectPacket.Initialize(packetAssemblies);
        }

        /// <summary>
        /// Register pre-generated MessagePack resolvers for AOT compiling.
        /// NOTE: This is only required for AOT.
        /// </summary>
        public static void RegisterMessagePackResolvers(params IFormatterResolver[] resolvers)
        {
            if (_serializerRegistered)
                return;

            Array.Resize(ref resolvers, resolvers.Length + 1);
            resolvers[resolvers.Length - 1] = StandardResolver.Instance;

            StaticCompositeResolver.Instance.Register(resolvers);

            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithResolver(StaticCompositeResolver.Instance);

            LZ4CompressOptions = MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithResolver(StaticCompositeResolver.Instance)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            _serializerRegistered = true;
        }
    }
}
