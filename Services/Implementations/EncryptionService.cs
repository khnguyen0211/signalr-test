using System.Collections.Concurrent;
using System.Security.Cryptography;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class EncryptionService : IEncryptionService
    {
        private static readonly Lazy<EncryptionService> _instance = new(() => new EncryptionService());
        public static EncryptionService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, byte[]> _encryptionKeys = new();

        private EncryptionService() { }

        public void SetEncryptionKey(string connectionId, string keyBase64)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(keyBase64);
                if (keyBytes.Length != 32) // AES-256 requires 32 bytes
                    throw new ArgumentException("Invalid key length. Expected 32 bytes for AES-256.");

                _encryptionKeys[connectionId] = keyBytes;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set encryption key: {ex.Message}");
            }
        }

        public byte[] DecryptChunk(string connectionId, byte[] encryptedData)
        {
            if (!_encryptionKeys.TryGetValue(connectionId, out var encryptionKey))
                throw new InvalidOperationException("Encryption key not found for connection");

            try
            {
                return DecryptWithAesGcm(encryptedData, encryptionKey);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Decryption failed: {ex.Message}");
            }
        }

        public void RemoveEncryptionKey(string connectionId)
        {
            _encryptionKeys.TryRemove(connectionId, out _);
        }

        private byte[] DecryptWithAesGcm(byte[] encryptedData, byte[] key)
        {
            const int ivLength = 12;
            const int tagLength = 16;

            if (encryptedData.Length < ivLength + tagLength)
                throw new ArgumentException("Encrypted data is too short");

            var iv = encryptedData.AsSpan(0, ivLength);
            var ciphertextWithTag = encryptedData.AsSpan(ivLength);
            var ciphertext = ciphertextWithTag[..^tagLength];
            var tag = ciphertextWithTag[^tagLength..];

            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(iv, ciphertext, tag, plaintext);

            return plaintext;
        }
    }
}
