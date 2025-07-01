namespace WarpBootstrap.Models
{
    public class ScriptSet
    {
        public ScriptInfo? PreInstallation { get; set; }
        public ScriptInfo? Installation { get; set; }
        public ScriptInfo? PostInstallation { get; set; }

        public bool IsValid => Installation != null && Installation.Exists;

        public List<ScriptInfo> GetOrderedScripts()
        {
            var scripts = new List<ScriptInfo>();

            if (PreInstallation?.Exists == true)
                scripts.Add(PreInstallation);

            if (Installation?.Exists == true)
                scripts.Add(Installation);

            if (PostInstallation?.Exists == true)
                scripts.Add(PostInstallation);

            return scripts;
        }
    }
}
