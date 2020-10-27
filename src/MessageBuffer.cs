﻿using System;
using System.Buffers;

namespace ByteTransfer
{
    public class MessageBuffer
    {
        public const int DefaultSize = 0x20;

        private int _wpos;
        private int _rpos;

        private byte[] _storage;

        public MessageBuffer(int initialSize)
        {
            _storage = ArrayPool<byte>.Shared.Rent(initialSize);
        }

        public MessageBuffer() : this(DefaultSize) { }

        public MessageBuffer(MessageBuffer right) : this(DefaultSize)
        {
            if (this == right) return;

            _wpos = right._wpos;
            _rpos = right._rpos;
            _storage = right.Move();
        }

        public byte[] Move()
        {
            _wpos = 0;
            _rpos = 0;

            var ret = _storage;
            _storage = null;

            return ret;
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public byte[] Data()
        {
            return _storage;
        }

        public byte GetByte(int pos)
        {
            return _storage[_rpos + pos];
        }

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            var temp = ArrayPool<byte>.Shared.Rent(bytes);
            Buffer.BlockCopy(_storage, 0, temp, 0, _storage.Length > temp.Length ? temp.Length : _storage.Length);

            ArrayPool<byte>.Shared.Return(_storage);

            _storage = temp;
        }

        public void ReadCompleted(int bytes)
        {
            _rpos += bytes;

            if (_wpos == 0)
                _rpos = 0;
        }

        public void WriteCompleted(int bytes) { _wpos += bytes; }

        public int GetActiveSize()
        {
            if (_rpos > _wpos)
                return 0;

            return _wpos - _rpos;
        }

        public int GetRemainingSpace() { return _storage.Length - _wpos; }

        public int GetBufferSize() { return _storage.Length; }

        // Discards inactive data
        public void Normalize()
        {
            if (_rpos > 0 && _wpos > _rpos)
            {
                if (_rpos != _wpos)
                    Array.ConstrainedCopy(_storage, _rpos, _storage, 0, GetActiveSize());

                _wpos -= _rpos;
                _rpos = 0;
            }
        }

        // Ensures there's "some" free space, make sure to call Normalize() before this
        public void EnsureFreeSpace()
        {
            // resize buffer if it's already full
            if (GetRemainingSpace() == 0)
                Resize(_storage.Length * 3 / 2);
        }

        public void Write(byte[] data, int size, int startIndex = 0)
        {
            if (size > 0)
            {
                Buffer.BlockCopy(data, startIndex, _storage, _wpos, size);
                WriteCompleted(size);
            }
        }
    }
}
