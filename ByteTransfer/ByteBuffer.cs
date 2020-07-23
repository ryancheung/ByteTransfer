using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

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
        public const int DEFAULT_SIZE = 0x1000;

        private static readonly ThreadLocal<byte[]> _ReadBuffer2Bytes;
        private static readonly ThreadLocal<byte[]> _ReadBuffer4Bytes;
        private static readonly ThreadLocal<byte[]> _ReadBuffer8Bytes;
        public static byte[] ReadBuffer2Bytes { get { return _ReadBuffer2Bytes.Value; } }
        public static byte[] ReadBuffer4Bytes { get { return _ReadBuffer4Bytes.Value; } }
        public static byte[] ReadBuffer8Bytes { get { return _ReadBuffer8Bytes.Value; } }

        static ByteBuffer()
        {
            _ReadBuffer2Bytes = new ThreadLocal<byte[]>(() => new byte[2]);
            _ReadBuffer4Bytes = new ThreadLocal<byte[]>(() => new byte[4]);
            _ReadBuffer8Bytes = new ThreadLocal<byte[]>(() => new byte[8]);
        }

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

        public ByteBuffer(ByteBuffer right) : this(DEFAULT_SIZE)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;

            Array.Copy(right._storage, _storage, right._storage.Length);
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

        public void Rfinish()
        {
            _rpos = _wpos;
        }

        public ReadOnlyCollection<byte> Data()
        {
            return Array.AsReadOnly(_storage);
        }

        public void Clear()
        {
            Array.Clear(_storage, 0, _storage.Length);
            _rpos = _wpos = 0;
        }

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

            Array.Copy(src, 0, _storage, _wpos, src.Length);

            _wpos = newSize;
        }

        public void Append(string src)
        {
            Append(Encoding.UTF8.GetBytes(src));
            Append((byte)0);
        }

        private void AppendWithEndianess(byte[] src)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(src);

            Append(src);
        }

        public void Append(byte src)
        {
            AppendWithEndianess(new byte[] { src });
        }

        public void Append(ushort src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(uint src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(ulong src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(sbyte src)
        {
            AppendWithEndianess(new byte[] { (byte)src });
        }

        public void Append(short src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(int src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(long src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(float src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Append(double src)
        {
            AppendWithEndianess(BitConverter.GetBytes(src));
        }

        public void Put(int pos, byte[] src)
        {
            Debug.Assert(pos + src.Length <= Size(), string.Format("Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", src.Length, pos, Size()));
            Debug.Assert(src != null, string.Format("Attempted to put a NULL-pointer in ByteBuffer (pos: {0} size: {1})", pos, Size()));

            Array.Copy(src, 0, _storage, pos, src.Length);
        }

        private void PutWithEndianess(int pos, byte[] src)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(src);

            Put(pos, src);
        }

        public void Put(int pos, byte src)
        {
            PutWithEndianess(pos, new byte[] { src });
        }

        public void Put(int pos, ushort src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, uint src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, ulong src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, sbyte src)
        {
            PutWithEndianess(pos, new byte[] { (byte)src });
        }

        public void Put(int pos, short src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, int src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, long src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, float src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public void Put(int pos, double src)
        {
            PutWithEndianess(pos, BitConverter.GetBytes(src));
        }

        public byte ReadByte()
        {
            return _storage[_rpos++];
        }

        public ushort ReadUShort()
        {
            Array.Copy(_storage, _rpos, ReadBuffer2Bytes, 0, 2);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer2Bytes);

            _rpos += 2;
            return BitConverter.ToUInt16(ReadBuffer2Bytes, 0);
        }

        public uint ReadUInt()
        {
            Array.Copy(_storage, _rpos, ReadBuffer4Bytes, 0, 4);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer4Bytes);

            _rpos += 4;
            return BitConverter.ToUInt32(ReadBuffer4Bytes, 0);
        }

        public ulong ReadULong()
        {
            Array.Copy(_storage, _rpos, ReadBuffer8Bytes, 0, 8);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer8Bytes);

            _rpos += 8;
            return BitConverter.ToUInt64(ReadBuffer8Bytes, 0);
        }

        public sbyte ReadSByte()
        {
            return (sbyte)_storage[_rpos++];
        }

        public short ReadShort()
        {
            Array.Copy(_storage, _rpos, ReadBuffer2Bytes, 0, 2);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer2Bytes);

            _rpos += 2;
            return BitConverter.ToInt16(ReadBuffer2Bytes, 0);
        }

        public int ReadInt()
        {
            Array.Copy(_storage, _rpos, ReadBuffer4Bytes, 0, 4);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer4Bytes);

            _rpos += 4;
            return BitConverter.ToInt32(ReadBuffer4Bytes, 0);
        }

        public long ReadLong()
        {
            Array.Copy(_storage, _rpos, ReadBuffer8Bytes, 0, 8);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer8Bytes);

            _rpos += 8;
            return BitConverter.ToInt64(ReadBuffer8Bytes, 0);
        }

        public float ReadFloat()
        {
            Array.Copy(_storage, _rpos, ReadBuffer4Bytes, 0, 4);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer4Bytes);

            _rpos += 4;

            var value = BitConverter.ToSingle(ReadBuffer4Bytes, 0);

            if (float.IsInfinity(value))
                throw new ByteBufferInvalidValueException("float", "infinity");

            return value;
        }

        public double ReadDouble()
        {
            Array.Copy(_storage, _rpos, ReadBuffer8Bytes, 0, 8);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ReadBuffer8Bytes);

            _rpos += 8;

            var value = BitConverter.ToDouble(ReadBuffer8Bytes, 0);

            if (double.IsInfinity(value))
                throw new ByteBufferInvalidValueException("double", "infinity");

            return value;
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

        public void Read(byte[] dest, int len, int destIndex = 0)
        {
            if (_rpos  + len > Size())
               throw new ByteBufferPositionException(false, _rpos, len, Size());

            Array.Copy(_storage, _rpos, dest, destIndex, len);

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

            sb.Append("STORAGE_SIZE: ").Append(Size());
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append(_storage[i]);
            }
            sb.Append(" ");
        }

        public void PrintTextLike()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE: ").Append(Size());
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append((char)_storage[i]);
            }
            sb.Append(" ");
        }

        public void PrintHexlike()
        {
            uint j = 1, k = 1;

            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE: ").Append(Size());

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

                sb.Append(ReadByte().ToString("X2"));
            }

            sb.Append(" ");
        }
    }
}
