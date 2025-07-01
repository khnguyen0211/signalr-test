namespace WarpBootstrap.Services.Interfaces
{
    public interface IEncryptionService
    {
        void SetEncryptionKey(string connectionId, string keyBase64);
        byte[] DecryptChunk(string connectionId, byte[] encryptedData);
        void RemoveEncryptionKey(string connectionId);
    }
}
