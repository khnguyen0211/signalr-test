namespace WarpBootstrap.Utilities
{
    public class EnvironmentConfiguration
    {
        public string ClientHost { get; private set; } = "";
        public string CertPath { get; private set; } = "";
        public string Password { get; private set; } = "";
        public int ServerPort { get; private set; } = 5001;

        public string Base64Key { get; private set; } = "";

        public static EnvironmentConfiguration Load(IConfiguration config)
        {
            return new EnvironmentConfiguration
            {
                ClientHost = config["ClientHost"] ?? "",
                CertPath = config["CertPath"] ?? "",
                Password = config["Password"] ?? "",
                ServerPort = int.TryParse(config["ServerPort"], out var port) ? port : 5001,
                Base64Key = config["BASE64_KEY"] ?? "",
            };
        }
    }

}
