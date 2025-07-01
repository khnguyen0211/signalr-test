using System.Collections.Concurrent;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class ConnectionManagerService : IConnectionManagerService
    {
        private static readonly Lazy<ConnectionManagerService> _instance = new(() => new ConnectionManagerService());
        public static ConnectionManagerService Instance => _instance.Value;
        private readonly ConcurrentDictionary<string, DateTime> _activeConnections = new();
        private readonly object _lockObject = new object();
        private const int MAX_CONNECTIONS = 1; // Only allow 1 connection

        private ConnectionManagerService() { }

        public bool TryRegisterConnection(string connectionId)
        {
            lock (_lockObject)
            {
                // Check if we already have the maximum number of connections
                if (_activeConnections.Count >= MAX_CONNECTIONS)
                {
                    Console.WriteLine($"[Connection Rejected] {connectionId} - Max connections ({MAX_CONNECTIONS}) reached");
                    return false;
                }

                // Register the new connection
                _activeConnections[connectionId] = DateTime.UtcNow;
                Console.WriteLine($"[Connection Accepted] {connectionId} - Total active: {_activeConnections.Count}");
                return true;
            }
        }

        public void UnregisterConnection(string connectionId)
        {
            lock (_lockObject)
            {
                if (_activeConnections.TryRemove(connectionId, out _))
                {
                    Console.WriteLine($"[Connection Removed] {connectionId} - Total active: {_activeConnections.Count}");
                }
            }
        }

        public bool IsConnectionActive(string connectionId)
        {
            return _activeConnections.ContainsKey(connectionId);
        }

        public IEnumerable<string> GetActiveConnections()
        {
            return _activeConnections.Keys.ToList();
        }

        public bool HasActiveConnection()
        {
            return _activeConnections.Count > 0;
        }

        public string? GetCurrentActiveConnection()
        {
            return _activeConnections.Keys.FirstOrDefault();
        }

        public int GetMaxAllowedConnections()
        {
            return MAX_CONNECTIONS;
        }
    }
}
