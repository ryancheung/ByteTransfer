using MessagePack;
using MessagePack.Resolvers;

namespace Shared
{
    public static class MessagePackManager
    {
        private static bool _serializerRegistered;

        public static void Register()
        {
            if (_serializerRegistered)
                return;

            StaticCompositeResolver.Instance.Register(
                 MessagePack.Resolvers.GeneratedResolver.Instance,
                 MessagePack.Resolvers.StandardResolver.Instance
            );

            var option = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);

            MessagePackSerializer.DefaultOptions = option;

            _serializerRegistered = true;
        }
    }
}
