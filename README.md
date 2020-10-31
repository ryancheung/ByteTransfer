# ByteTransfer

ByteTransfer is a opinioned multi-threaded object-based game networking implementation. Its goal is to transfer c# POCO Objects(annotated with [MessagePack](https://github.com/neuecc/MessagePack-CSharp)) with TCP protocal for game client and server.
It also gives the low level control to transfer data with raw bytes instead of C# objects, which means it can be also used along with game servers written by other language (e.g. c/cpp).

## Usage

1. First, created shared MessagePack packet classes, e.g:

```c#
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

namespace Shared.ServerPackets
{
    [MessagePackObject]
    public sealed class Login : ByteTransfer.ObjectPacket
    {
        [Key(0)]
        public string Result { get; set; }
    }
}
```

2. Implemented the Server:

```c#
namespace Server
{
    public class AuthSocket : ObjectSocket
    {
        public override void Start()
        {
            ServerSocket = true;

            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connection from {0}:{1} accepted", ipAddress, RemotePort);

            Session = new AuthSession(this);
            World.AddSession(Session);

            AsyncRead();
        }
    }

    public class AuthSession : ByteTransfer.BaseSession
    {
        public AuthSession(ObjectSocket socket) : base(socket) { }

        public void Process(Shared.ClientPackets.Login login)
        {
            Console.WriteLine("[AuthSession] Received login package, username: {0}, password: {1}", login.Username, login.Password);

            Socket.SendObjectPacket(new Shared.ServerPackets.Login { Result = login.Username }, true);
        }
    }

    public static class World 
    {
        private static List<BaseSession> Sessions = new List<BaseSession>();
        public static readonly ConcurrentQueue<BaseSession> NewSessions = new ConcurrentQueue<BaseSession>();

        public static void AddSession(BaseSession session)
        {
            NewSessions.Enqueue(session);
        }

        public static void Process()
        {
            BaseSession newSession;
            while(NewSessions.TryDequeue(out newSession))
                Sessions.Add(newSession);

            foreach(var s in Sessions)
                s.Process();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ByteTransfer.NetSettings.RegisterMessagePackResolvers(MessagePack.Resolvers.GeneratedResolver.Instance);
            ByteTransfer.NetSettings.InstallPackets(typeof(Shared.ClientPackets.Login).Assembly);

            var server = new ByteTransfer.NetServer<AuthSocket>("127.0.0.1", 3790);

            server.StartNetwork();

            while(true)
            {
                World.Process();
            }
        }
    }
}
```

2. Implemented the Client:

```c#
namespace Client
{
    public class ClientSocket : ObjectSocket
    {
        public override void Start()
        {
            SetBlocking(false);

            var ipAddress = RemoteAddress.ToString();
            Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

            Session = new ClientSession(this);

            var loginPacket = new Shared.ClientPackets.Login { Username = "foo", Password = "bar" };

            SendObjectPacket(loginPacket);

            AsyncRead();
        }
    }

    public class ClientSession : ByteTransfer.BaseSession
    {
        public ClientSession(ObjectSocket socket) : base(socket) { }

        public void Process(Shared.ServerPackets.Login login)
        {
            Console.WriteLine("[ClientSession] Received login package, result: {0}", login.Result);

            Socket.SendObjectPacket(new Shared.ClientPackets.Login { Username = new Random().Next(9999999, 999999999).ToString(), Password = "PWD" }, true);
            Thread.Sleep(10);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ByteTransfer.NetSettings.RegisterMessagePackResolvers(MessagePack.Resolvers.GeneratedResolver.Instance);
            ByteTransfer.NetSettings.InstallPackets(typeof(Shared.ClientPackets.Login).Assembly);

            Console.WriteLine("Connecting to 127.0.0.1:3790");

            var client = new ByteTransfer.NetClient<ClientSocket>("127.0.0.1", 3790);
            Thread.Sleep(100);

            while (true)
            {
                if (client.Socket != null)
                    client.Socket.Session.Process();
            }
        }
    }
}
```

Basically, to add packet handlers:

1. Add `Process(Your.Object.Packet object)` to your client/server session classes
2. Update your session by call `BaseSession.Process()` method in your game loop.

See the sample directory to get a full example.

## ARC4 Encrytion

Init AuthCrypt of ObjectSocket in your client and server socket class, e.g:

```c#
public class ClientSocket : ObjectSocket
{
    public override void Start()
    {
        SetBlocking(false);

        var ipAddress = RemoteAddress.ToString();
        Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

        Session = new ClientSession(this);

        _authCrypt.Init("key", Shared.Keys.ServerEncryptionKey, Shared.Keys.ClientEncryptionKey, ServerSocket);

        var loginPacket = new Shared.ClientPackets.Login { Username = "foo", Password = "bar" };

        SendObjectPacket(loginPacket);

        AsyncRead();
    }
}
```

## LZ4 compression for Object Packet

Method `ObjectSocket.SendObjectPacket` has an option to use lz4 compression, e.g: 

```c#
public class ClientSocket : ObjectSocket
{
    public override void Start()
    {
        SetBlocking(false);

        var ipAddress = RemoteAddress.ToString();
        Console.WriteLine("Connected to server {0}:{1}", ipAddress, RemotePort);

        Session = new ClientSession(this);

        var loginPacket = new Shared.ClientPackets.Login { Username = "foo", Password = "bar" };

        SendObjectPacket(loginPacket, true);

        AsyncRead();
    }
}
```
