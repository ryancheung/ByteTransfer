using System;
using System.Text;
using System.Security.Cryptography;

namespace ByteTransfer
{
    public class AuthCrypt
    {
        private byte[] _serverHmacKey;
        private byte[] _clientHmacKey;

        private string _sessionKey;
        private bool _server;

        private bool _initialized;
        public bool Initialized => _initialized;

        private ARC4 _decrypt = new ARC4();
        private ARC4 _encrypt = new ARC4();

        public void Init(string sessionKey, byte[] serverHmacKey, byte[] clientHmacKey, bool server = false)
        {
            _sessionKey = sessionKey;
            _serverHmacKey = serverHmacKey;
            _clientHmacKey = clientHmacKey;

            _server = server;

            if (_server)
            {
                HMACSHA1 hmacEncrypt = new HMACSHA1(_serverHmacKey);
                _encrypt.Key = hmacEncrypt.ComputeHash(Encoding.UTF8.GetBytes(_sessionKey));

                HMACSHA1 hmacDecrypt = new HMACSHA1(_clientHmacKey);
                _decrypt.Key = hmacDecrypt.ComputeHash(Encoding.UTF8.GetBytes(_sessionKey));
            }
            else
            {
                HMACSHA1 hmacEncrypt = new HMACSHA1(_clientHmacKey);
                _encrypt.Key = hmacEncrypt.ComputeHash(Encoding.UTF8.GetBytes(_sessionKey));

                HMACSHA1 hmacDecrypt = new HMACSHA1(_serverHmacKey);
                _decrypt.Key = hmacDecrypt.ComputeHash(Encoding.UTF8.GetBytes(_sessionKey));
            }

            _initialized = true;
        }

        public void DecryptRecv(byte[] data, int startIndex, int length)
        {
            if (!_initialized) return;

            _decrypt.UpdateData(data, startIndex, length);
        }

        public void EncryptSend(byte[] data, int startIndex, int length)
        {
            if (!_initialized) return;

            _encrypt.UpdateData(data, startIndex, length);
        }
    }
}
