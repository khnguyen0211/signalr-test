using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly string UploadFolder = Path.Combine(Path.GetTempPath(), "Warp/Compress");

        private static readonly ConcurrentDictionary<string, FileStream> ActiveUploads = new();

        private readonly IInstallerService _installerService;

        public ChatHub(IInstallerService installerService)
        {
            _installerService = installerService;
        }
        public async Task SendMessage(string message)
        {
            try
            {
                Console.WriteLine($"[Server] Message from {Context.ConnectionId}: {message}");
                await Clients.All.SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendMessage Error] {ex.Message}");
                throw;
            }
        }

        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"[Connected] {Context.ConnectionId} connected at {DateTime.Now}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[Disconnected] {Context.ConnectionId} disconnected at {DateTime.Now}");
            if (exception != null)
            {
                Console.WriteLine($"[Error] Disconnect reason: {exception.Message}");
            }
            return base.OnDisconnectedAsync(exception);
        }

        public async Task StartUpload(UploadMetaData metaData)
        {
            if (!Directory.Exists(UploadFolder))
                Directory.CreateDirectory(UploadFolder);

            string safeFileName = Path.GetFileName(metaData.FileName);
            string filePath = Path.Combine(UploadFolder, $"{Context.ConnectionId}_{safeFileName}");
            var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            ActiveUploads[Context.ConnectionId] = fs;
            await Clients.Caller.SendAsync("ReceiveMessage", $"Server ready to receive: {safeFileName}");
        }

        //public async Task UploadChunk(List<byte> chunk)
        //{
        //    if (ActiveUploads.TryGetValue(Context.ConnectionId, out var fs))
        //    {
        //        byte[] buffer = chunk.ToArray();
        //        await fs.WriteAsync(buffer, 0, buffer.Length);
        //    }
        //}

        private static readonly ConcurrentDictionary<string, byte[]> EncryptionKeys = new();

        public void SetEncryptionKey(string keyBase64)
        {
            var keyBytes = Convert.FromBase64String(keyBase64);
            EncryptionKeys.TryAdd(Context.ConnectionId, keyBytes);
        }

        public async Task UploadChunk(List<byte> encryptedChunk)
        {
            if (ActiveUploads.TryGetValue(Context.ConnectionId, out var fs) &&
                EncryptionKeys.TryGetValue(Context.ConnectionId, out var encryptionKey))
            {
                byte[] encryptedBuffer = encryptedChunk.ToArray();

                byte[] decryptedBuffer = DecryptChunk(encryptedBuffer, encryptionKey);

                await fs.WriteAsync(decryptedBuffer, 0, decryptedBuffer.Length);
            }
        }

        private byte[] DecryptChunk(byte[] encryptedData, byte[] key)
        {
            const int ivLength = 12;
            const int tagLength = 16;

            byte[] iv = new byte[ivLength];
            byte[] ciphertextWithTag = new byte[encryptedData.Length - ivLength];

            Array.Copy(encryptedData, 0, iv, 0, ivLength);
            Array.Copy(encryptedData, ivLength, ciphertextWithTag, 0, ciphertextWithTag.Length);

            byte[] ciphertext = new byte[ciphertextWithTag.Length - tagLength];
            byte[] tag = new byte[tagLength];

            Array.Copy(ciphertextWithTag, 0, ciphertext, 0, ciphertext.Length);
            Array.Copy(ciphertextWithTag, ciphertext.Length, tag, 0, tagLength);

            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            byte[] plaintext = new byte[ciphertext.Length];

            aes.Decrypt(iv, ciphertext, tag, plaintext);

            return plaintext;
        }


        public async Task EndUpload(Checksum checksumObj)
        {
            if (ActiveUploads.TryRemove(Context.ConnectionId, out var fs))
            {
                string savedFilePath = fs.Name;
                await fs.FlushAsync();
                fs.Close();
                await Clients.Caller.SendAsync("ReceiveMessage", "Upload completed and file saved.");

                Console.WriteLine($"Checksum Client: {checksumObj.ChecksumValue}");

                string checksum;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(savedFilePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var sb = new StringBuilder();
                    foreach (var b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    checksum = sb.ToString();
                }
                await Clients.Caller.SendAsync("ReceiveMessage", $"SHA-256 checksum: {checksum}");
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "No active upload to complete.");
            }
        }

    }

}
