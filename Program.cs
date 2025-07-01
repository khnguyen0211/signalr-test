using WarpBootstrap.Hubs;
using WarpBootstrap.Utilities;

var builder = WebApplication.CreateBuilder(args);


// Load environment variables
EnvironmentConfiguration envConfig = EnvironmentConfiguration.Load(builder.Configuration);

string clientHost = envConfig.ClientHost;
string certPath = envConfig.CertPath;
string password = envConfig.Password;
int serverPort = envConfig.ServerPort;

Console.WriteLine($"{clientHost} - {certPath} - {password} - {serverPort}");

// Configure Kestrel Server
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps(certPath, password);
    });
});

// Configure logging
builder.Logging.AddConsole();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(clientHost)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<ZipExtractor>();


// Register services
builder.Services.AddSignalR(e =>
{
    e.MaximumReceiveMessageSize = 52428800; //Maximum 50MB
});

var app = builder.Build();

// Middle-ware pipeline
app.UseRouting();
app.UseCors("AllowFrontend");

// Map SignalR hubs
app.MapHub<BootstrapHub>("/chatHub");

app.Run();
