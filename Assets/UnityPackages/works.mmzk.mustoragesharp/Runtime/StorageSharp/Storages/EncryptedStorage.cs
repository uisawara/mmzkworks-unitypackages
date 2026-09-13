using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    /// <summary>
    /// Encrypted storage class with AES encryption functionality.
    /// Encrypts and decrypts data when storing/reading from the inner IStorage.
    /// </summary>
    public class EncryptedStorage : IStorage
    {
        private readonly IStorage _innerStorage;
        private readonly byte[] _key;
        private readonly byte[] _iv;

        /// <summary>
        /// Initializes a new instance of the EncryptedStorage class.
        /// </summary>
        /// <param name="innerStorage">The inner storage to encrypt</param>
        /// <param name="password">The encryption password</param>
        public EncryptedStorage(IStorage innerStorage, string password)
        {
            _innerStorage = innerStorage ?? throw new ArgumentNullException(nameof(innerStorage));
            
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Generate key and IV from password
            using (var sha256 = SHA256.Create())
            {
                var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(passwordBytes);
                
                _key = new byte[32]; // 32-byte key for AES-256
                _iv = new byte[16];  // 16-byte IV for AES
                
                Array.Copy(hash, 0, _key, 0, 32);
                Array.Copy(hash, 0, _iv, 0, 16);
            }
        }

        /// <summary>
        /// Initializes a new instance of the EncryptedStorage class with specific key and IV.
        /// </summary>
        /// <param name="innerStorage">The inner storage to encrypt</param>
        /// <param name="key">AES encryption key (32 bytes)</param>
        /// <param name="iv">Initialization vector (16 bytes)</param>
        public EncryptedStorage(IStorage innerStorage, byte[] key, byte[] iv)
        {
            _innerStorage = innerStorage ?? throw new ArgumentNullException(nameof(innerStorage));
            
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes for AES-256", nameof(key));
            
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("IV must be 16 bytes for AES", nameof(iv));

            _key = new byte[32];
            _iv = new byte[16];
            Array.Copy(key, _key, 32);
            Array.Copy(iv, _iv, 16);
        }

        public UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            return _innerStorage.ListAll(cancellationToken);
        }

        public async UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            var encryptedData = await _innerStorage.ReadAsync(key, cancellationToken);
            return Decrypt(encryptedData);
        }

        public async UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (data == null || data.Length == 0)
            {
                await _innerStorage.WriteAsync(key, data, cancellationToken);
                return;
            }

            var encryptedData = Encrypt(data);
            await _innerStorage.WriteAsync(key, encryptedData, cancellationToken);
        }

        public async UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            var encryptedData = await _innerStorage.ReadAsync(key, cancellationToken);
            var decryptedData = Decrypt(encryptedData);
            return new MemoryStream(decryptedData);
        }

        public async UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var data = memoryStream.ToArray();

            await WriteAsync(key, data, cancellationToken);
        }

        /// <summary>
        /// Encrypts the specified data.
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <returns>The encrypted data</returns>
        private byte[] Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();
            
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Decrypts the specified encrypted data.
        /// </summary>
        /// <param name="encryptedData">The encrypted data to decrypt</param>
        /// <returns>The decrypted data</returns>
        private byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return encryptedData;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var memoryStream = new MemoryStream(encryptedData);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var resultStream = new MemoryStream();
            
            cryptoStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
} 