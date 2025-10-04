using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.IO;

namespace SecretStore.Security
{
    class CryptoUtils
    {
        private const int KeySizeBytes = 32;
        private const int SaltSizeBytes = 16;
        private const int IvSizeBytes = 16;
        private const int Iterations = 100000;

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(KeySizeBytes);
            }
        }

        public static string EncryptString(string plainText, string password)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            var salt = new byte[SaltSizeBytes];
            RandomNumberGenerator.Create().GetBytes(salt);

            var key = DeriveKey(password, salt);

            var iv = new byte[IvSizeBytes];
            RandomNumberGenerator.Create().GetBytes(iv);

            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream())
                using (var crypto = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    crypto.Write(plainBytes, 0, plainBytes.Length);
                    crypto.FlushFinalBlock();
                    cipher = ms.ToArray();
                }
            }

            // HMAC-SHA256 для проверки целостности
            byte[] tag;
            using (var hmac = new HMACSHA256(key))
            {
                tag = hmac.ComputeHash(cipher);
            }

            using (var outMs = new MemoryStream())
            {
                outMs.Write(salt, 0, salt.Length);
                outMs.Write(iv, 0, iv.Length);
                outMs.Write(tag, 0, tag.Length);
                outMs.Write(cipher, 0, cipher.Length);

                return Convert.ToBase64String(outMs.ToArray());
            }
        }

        public static string DecryptString(string combinedBase64, string password)
        {
            var all = Convert.FromBase64String(combinedBase64);

            var salt = new byte[SaltSizeBytes];
            var iv = new byte[IvSizeBytes];
            var tag = new byte[32];

            Buffer.BlockCopy(all, 0, salt, 0, salt.Length);
            Buffer.BlockCopy(all, salt.Length, iv, 0, iv.Length);
            Buffer.BlockCopy(all, salt.Length + iv.Length, tag, 0, tag.Length);

            var cipher = new byte[all.Length - salt.Length - iv.Length - tag.Length];
            Buffer.BlockCopy(all, salt.Length + iv.Length + tag.Length, cipher, 0, cipher.Length);

            var key = DeriveKey(password, salt);

            // Проверка HMAC
            using (var hmac = new HMACSHA256(key))
            {
                var realTag = hmac.ComputeHash(cipher);
                if (!AreEqual(realTag, tag))
                    throw new CryptographicException("HMAC mismatch — неверный мастер-пароль или повреждён файл");
            }

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream(cipher))
                using (var crypto = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var reader = new StreamReader(crypto, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
