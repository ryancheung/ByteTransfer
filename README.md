# ByteTransfer

ByteTransfer is a multi-threaded TCP client for game development.
It gives low level controls on game networking. It transfers/receives data with raw bytes instead of C# objects,
which means it can be used along with game servers written by other language (e.g. c/cpp) or C#.

## Why

When you want to write your mmo game server with byte protocal for networking, e.g. [TrinityCore](https://github.com/TrinityCore/TrinityCore).
And you have a game client written with C# based game engine, e.g. Unity/MonoGame.

## Usage

First, create a class that implements BaseSocket:

```c#
public class AuthSession : BaseSocket
{
    public void SendPacket(ByteBuffer packet)
    {
        if (!IsOpen()) return;

        if (!packet.Empty)
        {
            var buffer = new MessageBuffer(packet.Size());
            buffer.Write(packet.Data(), packet.Size());
            QueuePacket(buffer);
        }
    }

    public override void Start()
    {
        SetBlocking(false);

        var ipAddress = RemoteAddress.ToString();
        Console.WriteLine("Connected to {0}", ipAddress);

        var buffer = new ByteBuffer(10);
        buffer.Append((byte)0);
        buffer.Append((ushort)10);
        buffer.Append("username");
        buffer.Append("password");
        buffer.Append(true);
        SendPacket(buffer);

        AsyncRead();
    }

    public const byte LOGON_CMD = 0;
    public const int LOGON_RESULT_PACKET_SIZE = 10;

    protected override void ReadHandler()
    {
        MessageBuffer packet = GetReadBuffer();

        while (packet.GetActiveSize() > 0)
        {
            byte cmd = packet.Data()[0];

            if (cmd == LOGON_CMD)
            {
                if (packet.GetActiveSize() < LOGON_RESULT_PACKET_SIZE)
                    break;
            }

            var result = BitConverter.ToUInt16(packet.Data(), packet.Rpos());
            packet.ReadCompleted(sizeof(ushort));

            var code = BitConverter.ToInt32(packet.Data(), packet.Rpos());
            packet.ReadCompleted(sizeof(int));
            ...

            if (!HandleLogonResult(result, code))
            {
                // Failed to login.
                CloseSocket();
                return;
            }
        }

        AsyncRead();
    }

    private bool HandleLogonResult(ushort result, int code)
    {
        ...
    }
}
```

Then, intialize a client to connect to a remote server:

```c#
static void Main(string[] args)
{
    Console.WriteLine("Connecting to 127.0.0.1:3790");

    var client = new ByteTransfer.Client<AuthSession>("127.0.0.1", 3790);

    while(true)
    {
        Thread.Sleep(10);
    }
}
```
