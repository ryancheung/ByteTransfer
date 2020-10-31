using System;
using System.Buffers;

namespace ByteTransfer
{
    /// <summary>
    /// The RC4 encryption algorithm used to encrypt packet headers
    /// </summary>
    public class ARC4
    {
        /// <summary>
        /// <para>Give data and an encryption key, apply RC4 cryptography.  RC4 is symmetric,
        /// which means this single method will work for encrypting and decrypting.</para>
        /// <para>Note it updates data in place.</para>
        /// </summary>
        /// <remarks>
        ///     https://en.wikipedia.org/wiki/RC4
        /// </remarks>
        /// <param name="data">
        ///     Byte array representing the data to be encrypted/decrypted
        /// </param>
        /// <param name="key">
        ///     Byte array representing the key to use
        /// </param>
        public static void Apply(byte[] data, byte[] key, int startIndex = 0, int length = 0)
        {
            //  Key Scheduling Algorithm Phase:
            //  KSA Phase Step 1: First, the entries of S are set equal to the values of 0 to 255 
            //                    in ascending order.
            int[] S = ArrayPool<int>.Shared.Rent(256);
            for (int _ = 0; _ < 256; _++)
            {
                S[_] = _;
            }

            //  KSA Phase Step 2a: Next, a temporary vector T is created.
            int[] T = ArrayPool<int>.Shared.Rent(256);

            //  KSA Phase Step 2b: If the length of the key k is 256 bytes, then k is assigned to T.  
            if (key.Length == 256)
            {
                Buffer.BlockCopy(key, 0, T, 0, key.Length);
            }
            else
            {
                //  Otherwise, for a key with a given length, copy the elements of
                //  the key into vector T, repeating for as many times as neccessary to
                //  fill T
                for (int _ = 0; _ < 256; _++)
                {
                    T[_] = key[_ % key.Length];
                }
            }

            //  KSA Phase Step 3: We use T to produce the initial permutation of S ...
            int i = 0;
            int j = 0;
            for (i = 0; i < 256; i++)
            {
                //  increment j by the sum of S[i] and T[i], however keeping it within the 
                //  range of 0 to 255 using mod (%) division.
                j = (j + S[i] + T[i]) % 256;

                //  Swap the values of S[i] and S[j]
                int temp = S[i];
                S[i] = S[j];
                S[j] = temp;
            }

            ArrayPool<int>.Shared.Return(T);

            var dataLength = length > 0 ? Math.Min(length, data.Length) : data.Length;

            //  Pseudo random generation algorithm (Stream Generation):
            //  Once the vector S is initialized from above in the Key Scheduling Algorithm Phase,
            //  the input key is no longer used.  In this phase, for the length of the data, we ...
            i = j = 0;
            byte[] result = ArrayPool<byte>.Shared.Rent(dataLength);
            for (int iteration = 0; iteration < dataLength; iteration++)
            {
                //  PRGA Phase Step 1. Continously increment i from 0 to 255, starting it back 
                //                     at 0 once we go beyond 255 (this is done with mod (%) division
                i = (i + 1) % 256;

                //  PRGA Phase Step 2. Lookup the i'th element of S and add it to j, keeping the
                //                     result within the range of 0 to 255 using mod (%) division
                j = (j + S[i]) % 256;

                //  PRGA Phase Step 3. Swap the values of S[i] and S[j]
                int temp = S[i];
                S[i] = S[j];
                S[j] = temp;

                //  PRGA Phase Step 4. Use the result of the sum of S[i] and S[j], mod (%) by 256, 
                //                     to get the index of S that handls the value of the stream value K.
                int K = S[(S[i] + S[j]) % 256];

                //  PRGA Phase Step 5. Use bitwise exclusive OR (^) with the next byte in the data to
                //                     produce  the next byte of the resulting ciphertext (when 
                //                     encrypting) or plaintext (when decrypting)
                result[iteration] = Convert.ToByte(data[iteration+startIndex] ^ K);
            }

            ArrayPool<int>.Shared.Return(S);

            Buffer.BlockCopy(result, 0, data, startIndex, dataLength);

            ArrayPool<byte>.Shared.Return(result);
        }

        private byte[] _key;
        public byte[] Key
        {
            get { return _key; }
            set { _key = value; }
        }

        public void UpdateData(byte[] data, int startIndex, int length)
        {
            Apply(data, _key, startIndex, length);
        }
    }
}
