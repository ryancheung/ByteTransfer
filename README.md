# ByteTransfer

ByteTransfer is a multi-threaded TCP client for game development.
It gives low level controls on game networking. It transfers/receives data with raw bytes instead of C# objects,
which means it can be used along with game servers written by other language (e.g. c/cpp) or C#.

## Why

When you want to write your mmo game server with byte protocal for networking, e.g. [TrinityCore](https://github.com/TrinityCore/TrinityCore)
And you have a game client written with C# based game engine, e.g. Unity/MonoGame.
