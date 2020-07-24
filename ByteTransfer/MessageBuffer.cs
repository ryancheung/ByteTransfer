using System;
using System.Collections.ObjectModel;

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
            _storage = new byte[initialSize];
        }

        public MessageBuffer() : this(DefaultSize) { }

        public MessageBuffer(MessageBuffer right) : this(DefaultSize)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;

            Array.Copy(right._storage, _storage, right._storage.Length);
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public byte[] Data()
        {
            return _storage;
        }

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            Array.Resize(ref _storage, bytes);
        }

        public void ReadCompleted(int bytes) { _rpos += bytes; }

        public void WriteCompleted(int bytes) { _wpos += bytes; }

        public int GetActiveSize() { return _wpos - _rpos; }

        public int GetRemainingSpace() { return _storage.Length - _wpos; }

        public int GetBufferSize() { return _storage.Length; }

        // Discards inactive data
        public void Normalize()
        {
            if (_rpos > 0)
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
                Array.Resize(ref _storage, _storage.Length * 3 / 2);
        }

        public void Write(byte[] data, int size, int startIndex = 0)
        {
            if (size > 0)
            {
                Array.Copy(data, startIndex, _storage, _wpos, size);
                WriteCompleted(size);
            }
        }
    }
}
