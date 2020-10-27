using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Buffers;

namespace ByteTransfer
{
    public class ByteBufferException : Exception
    {
        public override string Message { get; }
    }

    public class ByteBufferPositionException : ByteBufferException
    {
        public override string Message { get; }

        public ByteBufferPositionException(bool add, int pos, int size, int valueSize)
        {
            var sb = new StringBuilder();
            sb.Append("Attempted to ");
            sb.Append(add ? "put" : "get");
            sb.Append(" value with size: ");
            sb.Append(valueSize);
            sb.Append(" in ByteBuffer (pos: ");
            sb.Append(pos);
            sb.Append(" size: ");
            sb.Append(size);
            sb.Append(")");

            Message = sb.ToString();
        }
    }

    public class ByteBufferSourceException : ByteBufferException
    {
        public override string Message { get; }

        ByteBufferSourceException(int pos, int size, int valueSize)
        {
            var sb = new StringBuilder();
            sb.Append("Attempted to put a ");
            sb.Append(valueSize > 0 ? "NULL-pointer" : "zero-sized value");
            sb.Append(" in ByteBuffer (pos: ");
            sb.Append(pos);
            sb.Append(" size: ");
            sb.Append(size);
            sb.Append(")");

            Message = sb.ToString();
        }
    }

    public class ByteBufferInvalidValueException : ByteBufferException
    {
        public override string Message { get; }

        public ByteBufferInvalidValueException(string type, string value)
        {
            Message = string.Format("Invalid {0} value ({1}) found in ByteBuffer", type, value);
        }
    }

    public class ByteBuffer
    {
        public const int DEFAULT_SIZE = 0x20;


        private int _wpos;
        private int _rpos;

        private byte[] _storage;

        public ByteBuffer(int initialSize)
        {
            _storage = new byte[initialSize];
        }

        public ByteBuffer() : this(DEFAULT_SIZE)
        {
        }

        public ByteBuffer(ByteBuffer right)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;
            _storage = right._storage;

            right._storage = new byte[0];
            right._wpos = 0;
            right._rpos = 0;
        }

        public ByteBuffer(MessageBuffer buffer)
        {
            _rpos = 0;
            _wpos = 0;
            _storage = buffer.Move();
        }

        public ByteBuffer(byte[] src, int startIndex, int length)
        {
            _rpos = 0;
            _wpos = length;
            _storage = new byte[length];

            Buffer.BlockCopy(src, startIndex, _storage, 0, length);
        }

        public int Size()
        {
            return _storage.Length;
        }
        public void Resize(int newsize)
        {
            Array.Resize(ref _storage, newsize);
            _rpos = 0;
            _wpos = Size();
        }

        public int this[int pos]
        {
            get
            {
                if (pos > Size())
                    throw new ByteBufferPositionException(false, pos, 1, Size());

                return _storage[pos];
            }
        }

        public int Rpos() { return _rpos; }
        public int Rpos(int pos)
        {
            _rpos = pos;
            return _rpos;
        }

        public int Wpos() { return _wpos; }
        public int Wpos(int pos)
        {
            _wpos = pos;
            return _wpos;
        }

        public void FinishRead()
        {
            _rpos = _wpos;
        }

        public byte[] Data()
        {
            return _storage;
        }

        public void Clear()
        {
            Array.Clear(_storage, 0, _storage.Length);
            _rpos = _wpos = 0;
        }

        public bool Empty { get { return _storage.Length == 0; } }

        /// <summary>
        /// A lookup of type sizes. Used instead of Marshal.SizeOf() which has additional
        /// overhead, but also is compatible with generic functions for simplified code.
        /// </summary>
        private static Dictionary<Type, int> GenericSizes = new Dictionary<Type, int>()
        {
            { typeof(bool),     sizeof(bool) },
            { typeof(float),    sizeof(float) },
            { typeof(double),   sizeof(double) },
            { typeof(sbyte),    sizeof(sbyte) },
            { typeof(byte),     sizeof(byte) },
            { typeof(short),    sizeof(short) },
            { typeof(ushort),   sizeof(ushort) },
            { typeof(int),      sizeof(int) },
            { typeof(uint),     sizeof(uint) },
            { typeof(ulong),    sizeof(ulong) },
            { typeof(long),     sizeof(long) },
        };

        /// <summary>
        /// Get the wire-size (in bytes) of a type supported by flatbuffers.
        /// </summary>
        /// <param name="t">The type to get the wire size of</param>
        /// <returns></returns>
        public static int SizeOf<T>()
        {
            return GenericSizes[typeof(T)];
        }

        /// <summary>
        /// Checks if the Type provided is supported as scalar value
        /// </summary>
        /// <typeparam name="T">The Type to check</typeparam>
        /// <returns>True if the type is a scalar type that is supported, falsed otherwise</returns>
        public static bool IsSupportedType<T>()
        {
            return GenericSizes.ContainsKey(typeof(T));
        }

        public void Append(byte[] src)
        {
            Debug.Assert(src != null, string.Format("Attempted to put a NULL-pointer in ByteBuffer (pos: {0} size: {1})", _wpos, Size()));
            Debug.Assert(src.Length > 0, string.Format("Attempted to put a zero-sized value in ByteBuffer (pos: {0} size: {1})", _wpos, Size()));
            Debug.Assert(Size() < 10000000);

            int newSize = _wpos + src.Length;
            if (_storage.Length < newSize) // custom memory allocation rules
            {
                if (newSize < 100)
                    Array.Resize(ref _storage, 300);
                else if (newSize < 750)
                    Array.Resize(ref _storage, 2500);
                else if (newSize < 6000)
                    Array.Resize(ref _storage, 10000);
                else
                    Array.Resize(ref _storage, 400000);
            }

            if (_storage.Length < newSize)
                Array.Resize(ref _storage, newSize);

            Buffer.BlockCopy(src, 0, _storage, _wpos, src.Length);

            _wpos = newSize;
        }

        public void Append(string value)
        {
            Append(Encoding.UTF8.GetBytes(value));
            Append((byte)0);
        }

        public void Append(bool value)
        {
            Append(new byte[] { value ? (byte)1 : (byte)0 });
        }

        public void Append(byte value)
        {
            Append(new byte[] { value });
        }

        public void Append(ushort value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(uint value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(ulong value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(sbyte value)
        {
            Append(new byte[] { (byte)value });
        }

        public void Append(short value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(int value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(long value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(float value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Append(double value)
        {
            Append(BitConverter.GetBytes(value));
        }

        public void Put(int pos, byte[] src, int startIndex, int length)
        {
            Debug.Assert(pos + length <= Size(), string.Format("Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", length, pos, Size()));
            Debug.Assert(src != null, string.Format("Attempted to put a NULL-pointer in ByteBuffer (pos: {0} size: {1})", pos, Size()));

            Buffer.BlockCopy(src, startIndex, _storage, pos, length);
        }

        public void Put(int pos, byte[] src, int startIndex = 0)
        {
            Debug.Assert(pos + src.Length <= Size(), string.Format("Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", src.Length, pos, Size()));
            Debug.Assert(src != null, string.Format("Attempted to put a NULL-pointer in ByteBuffer (pos: {0} size: {1})", pos, Size()));

            Buffer.BlockCopy(src, startIndex, _storage, pos, src.Length);
        }

        public void Put(int pos, byte value)
        {
            Put(pos, new byte[] { value });
        }

        public void Put(int pos, bool value)
        {
            Put(pos, new byte[] { value ? (byte)1 : (byte)0 });
        }

        public void Put(int pos, ushort value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, uint value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, ulong value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, sbyte value)
        {
            Put(pos, new byte[] { (byte)value });
        }

        public void Put(int pos, short value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, int value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, long value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, float value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public void Put(int pos, double value)
        {
            Put(pos, BitConverter.GetBytes(value));
        }

        public bool ReadBool()
        {
            return _storage[_rpos++] > 0 ? true : false;
        }

        public byte ReadByte()
        {
            return _storage[_rpos++];
        }

        public ushort ReadUShort()
        {
            var temp = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 2);

                _rpos += 2;
                return BitConverter.ToUInt16(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public uint ReadUInt()
        {
            var temp = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 4);

                _rpos += 4;
                return BitConverter.ToUInt32(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public ulong ReadULong()
        {
            var temp = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 8);

                _rpos += 8;
                return BitConverter.ToUInt64(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public sbyte ReadSByte()
        {
            return (sbyte)_storage[_rpos++];
        }

        public short ReadShort()
        {
            var temp = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 2);

                _rpos += 2;
                return BitConverter.ToInt16(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public int ReadInt()
        {
            var temp = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 4);

                _rpos += 4;
                return BitConverter.ToInt32(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public long ReadLong()
        {
            var temp = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 8);

                _rpos += 8;
                return BitConverter.ToInt64(temp, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public float ReadFloat()
        {
            var temp = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 4);

                _rpos += 4;

                var value = BitConverter.ToSingle(temp, 0);

                if (float.IsInfinity(value))
                    throw new ByteBufferInvalidValueException("float", "infinity");

                return value;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public double ReadDouble()
        {
            var temp = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                Buffer.BlockCopy(_storage, _rpos, temp, 0, 8);

                _rpos += 8;

                var value = BitConverter.ToDouble(temp, 0);

                if (double.IsInfinity(value))
                    throw new ByteBufferInvalidValueException("double", "infinity");

                return value;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public string ReadString()
        {
            var index = _rpos;
            var len = 0;

            while (_rpos < Size())
            {
                var c = ReadByte();

                if (c == 0)
                    break;

                len++;
            }

            if (len > 0)
                return Encoding.UTF8.GetString(_storage, index, len);
            else
                return string.Empty;
        }

        public byte[] ReadBytes(int len)
        {
            if (_rpos + len > Size())
                throw new ByteBufferPositionException(false, _rpos, len, Size());

            byte[] bytes = new byte[len];

            Buffer.BlockCopy(_storage, _rpos, bytes, 0, len);

            _rpos += len;

            return bytes;
        }

        public void ReadBytes(byte[] dest, int len, int destIndex = 0)
        {
            if (_rpos + len > Size())
                throw new ByteBufferPositionException(false, _rpos, len, Size());

            Buffer.BlockCopy(_storage, _rpos, dest, destIndex, len);

            _rpos += len;
        }

        public void ReadSkip(int skip)
        {
            if (_rpos + skip > Size())
                throw new ByteBufferPositionException(false, _rpos, skip, Size());

            _rpos += skip;
        }

        public void ReadSkip<T>() where T : unmanaged { ReadSkip(SizeOf<T>()); }
        public void ReadSkipString()
        {
            while (_rpos < Size())
            {
                var c = ReadByte();

                if (c == 0)
                    break;
            }
        }

        public void PrintStorage()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append(_storage[i]).Append(" - ");
            }
            sb.Append(" ");

            Console.WriteLine(sb.ToString());
        }

        public void PrintTextLike()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append((char)_storage[i]);
            }
            sb.Append(" ");

            Console.WriteLine(sb.ToString());
        }

        public void PrintHexlike()
        {
            uint j = 1, k = 1;

            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");

            for (uint i = 0; i < Size(); ++i)
            {
                if ((i == (j * 8)) && ((i != (k * 16))))
                {
                    sb.Append("| ");
                    ++j;
                }
                else if (i == (k * 16))
                {
                    sb.Append("\n");
                    ++k;
                    ++j;
                }

                sb.Append("0x").Append(ReadByte().ToString("X2")).Append(" ");
            }

            sb.Append(" ");

            Console.WriteLine(sb.ToString());
        }
    }
}
