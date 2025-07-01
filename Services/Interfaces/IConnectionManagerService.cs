namespace WarpBootstrap.Services.Interfaces
{
    public interface IConnectionManagerService
    {
        bool TryRegisterConnection(string connectionId);
        void UnregisterConnection(string connectionId);
        bool IsConnectionActive(string connectionId);
        IEnumerable<string> GetActiveConnections();
        bool HasActiveConnection();
        string? GetCurrentActiveConnection();
        int GetMaxAllowedConnections();
    }
}
