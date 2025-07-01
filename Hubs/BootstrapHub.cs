using Microsoft.AspNetCore.SignalR;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Implementations;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Hubs
{
    public class BootstrapHub : Hub
    {
        private readonly IFileUploadService _fileUploadService;
        private readonly IEncryptionService _encryptionService;
        private readonly IConnectionManagerService _connectionManager;
        private readonly IExtractionService _extractionService;
        private readonly IScriptExecutionService _scriptExecutionService;

        public BootstrapHub()
        {
            _fileUploadService = FileUploadService.Instance;
            _encryptionService = EncryptionService.Instance;
            _connectionManager = ConnectionManagerService.Instance;
            _extractionService = ExtractionService.Instance;
            _scriptExecutionService = ScriptExecutionService.Instance;
        }

        public async Task SendMessage(string message)
        {
            try
            {
                Console.WriteLine($"[Server] Message from {Context.ConnectionId}: {message}");
                // Only send to the current active connection (single client)
                await Clients.Caller.SendAsync("ReceiveMessage", $"Echo: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendMessage Error] {ex.Message}");
                await Clients.Caller.SendAsync("ReceiveMessage", $"Error: {ex.Message}");
            }
        }

        public async Task StartUpload(UploadMetaData metaData)
        {
            try
            {
                // Verify this connection is registered and active
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized for upload");
                    return;
                }

                string message = _fileUploadService.StartUploadAsync(Context.ConnectionId, metaData);
                await Clients.Caller.SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Upload start failed: {ex.Message}");
            }
        }

        public void SetEncryptionKey(string keyBase64)
        {
            try
            {
                // Verify this connection is registered and active
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized");
                    return;
                }

                _encryptionService.SetEncryptionKey(Context.ConnectionId, keyBase64);
            }
            catch (Exception ex)
            {
                Clients.Caller.SendAsync("ReceiveMessage", $"Encryption key setup failed: {ex.Message}");
            }
        }

        public async Task UploadChunk(List<byte> encryptedChunk)
        {
            try
            {
                // Verify this connection is registered and active
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized for upload");
                    return;
                }

                await _fileUploadService.ProcessChunkAsync(Context.ConnectionId, encryptedChunk);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Chunk processing failed: {ex.Message}");
            }
        }

        public async Task EndUpload(Checksum checksumObj)
        {
            try
            {
                // Verify this connection is registered and active
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized for upload");
                    return;
                }

                var message = await _fileUploadService.EndUploadAsync(Context.ConnectionId, checksumObj);
                await Clients.Caller.SendAsync("ReceiveMessage", message);

                var extractPath = $"{message}\\python";
                var request = new ScriptExecutionRequest
                {
                    Version = "python3.11",
                    OS = "windows",
                    ExtractedPath = extractPath
                };

                var s = await StartScriptExecution(request);

                await Clients.Caller.SendAsync("ReceiveMessage", s);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Upload completion failed: {ex.Message}");
            }
        }

        // New extraction-related methods
        public async Task GetArchiveInfo(string fileName)
        {
            try
            {
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized");
                    return;
                }

                var filePath = Path.Combine(Path.GetTempPath(), "Warp", "Compress", $"{Context.ConnectionId}_{fileName}");

                if (!File.Exists(filePath))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: File not found");
                    return;
                }

                var archiveInfo = await _extractionService.GetArchiveInfoAsync(filePath);
                await Clients.Caller.SendAsync("ArchiveInfo", archiveInfo);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Failed to get archive info: {ex.Message}");
            }
        }

        public async Task GetExtractionProgress()
        {
            try
            {
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized");
                    return;
                }

                var extractionService = ExtractionService.Instance;
                var progress = extractionService.GetExtractionProgress(Context.ConnectionId);

                if (progress != null)
                {
                    await Clients.Caller.SendAsync("ExtractionProgress", progress);
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "No extraction in progress");
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Failed to get extraction progress: {ex.Message}");
            }
        }

        public override async Task OnConnectedAsync()
        {
            // Try to register this connection
            bool isAccepted = _connectionManager.TryRegisterConnection(Context.ConnectionId);

            if (!isAccepted)
            {
                // Reject the connection
                await Clients.Caller.SendAsync("ReceiveMessage",
                    $"Connection rejected: Only {_connectionManager.GetMaxAllowedConnections()} client(s) allowed at a time");

                // Forcefully close the connection
                Context.Abort();
                return;
            }

            // Connection accepted
            Console.WriteLine($"[Connected] {Context.ConnectionId} connected at {DateTime.Now}");
            await Clients.Caller.SendAsync("ReceiveMessage", "Connection established. You are the only client connected.");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up all resources for this connection
            await _fileUploadService.CleanupConnectionAsync(Context.ConnectionId);
            _connectionManager.UnregisterConnection(Context.ConnectionId);

            Console.WriteLine($"[Disconnected] {Context.ConnectionId} disconnected at {DateTime.Now}");
            if (exception != null)
            {
                Console.WriteLine($"[Error] Disconnect reason: {exception.Message}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Optional: Admin method to check connection status
        public async Task GetConnectionStatus()
        {
            try
            {
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized");
                    return;
                }

                var activeConnections = _connectionManager.GetActiveConnections().Count();
                var maxConnections = _connectionManager.GetMaxAllowedConnections();

                await Clients.Caller.SendAsync("ReceiveMessage",
                    $"Connection Status: {activeConnections}/{maxConnections} active connections");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Status check failed: {ex.Message}");
            }
        }

        public async Task<string> StartScriptExecution(ScriptExecutionRequest request)
        {
            try
            {
                if (!_connectionManager.IsConnectionActive(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Error: Connection not authorized");
                    return string.Empty;
                }

                Console.WriteLine($"[Hub] Starting script execution for {request.Version}/{request.OS}");

                // Start execution in background
                _ = Task.Run(async () =>
                {
                    await _scriptExecutionService.ExecuteInstallationAsync(
                        request.Version,
                        request.OS,
                        request.ExtractedPath,
                        Context.ConnectionId
                    );
                });

                await Clients.Caller.SendAsync("ReceiveMessage",
                    $"Script execution started for {request.Version} on {request.OS}");

                return "Execution started";
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", $"Failed to start execution: {ex.Message}");
                return string.Empty;
            }
        }
    }

}
