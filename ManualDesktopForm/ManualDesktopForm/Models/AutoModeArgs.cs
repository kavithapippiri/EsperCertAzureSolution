namespace ManualDesktopForm.Models
{
    public class AutoModeArgs
    {
        public bool Auto { get; }
        public string PfxPassword { get; }
        public string EsperGroup { get; }
        public string CertAlias { get; }

        public AutoModeArgs(string[] args)
        {
            Auto = args.Contains("--auto", StringComparer.OrdinalIgnoreCase);
            //PfxPassword = GetArgValue(args, "--pfx");
            //EsperGroup = GetArgValue(args, "--group");
            //CertAlias = GetArgValue(args, "--alias");
        }

        private static string GetArgValue(string[] args, string key)
        {
            var index = Array.FindIndex(args, a => a.Equals(key, StringComparison.OrdinalIgnoreCase));
            return (index >= 0 && index + 1 < args.Length) ? args[index + 1] : string.Empty;
        }
    }
}
