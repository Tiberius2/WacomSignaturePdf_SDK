using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace WacomSignaturePdf.Config
{
    public static class AppConfig
    {
        private static readonly Configuration _config = LoadDllConfig();

        public static readonly string WorkingRoot = ResolveFromEnvOrConfig("RecruitmentDocsPath", "WorkingRoot");
        public static readonly string FreeFormDocumentsPath = ResolveFromEnvOrConfig("FreeFormDocumentsPath", "FreeFormDocumentsPath");
        public static readonly string TemplatesDir = ResolveTemplatesDir();

        // Environment variable takes priority over app.config (allows per-machine GPO deployment).
        private static string ResolveFromEnvOrConfig(string envVar, string configKey)
        {
            string env = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(env)) return env;

            string config = Get(configKey, null);
            return !string.IsNullOrWhiteSpace(config) ? config : null;
        }

        private static string ResolveTemplatesDir()
        {
            string envPath = Environment.GetEnvironmentVariable("TemplateDocsPath");
            if (!string.IsNullOrWhiteSpace(envPath))
                return Path.Combine(envPath, "Sabloane Semnaturi Electronice");

            string configDir = Get("TemplatesDir", "Document Templates");
            if (string.IsNullOrWhiteSpace(configDir)) return null;

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string baseDir = !string.IsNullOrWhiteSpace(assemblyPath)
                ? Path.GetDirectoryName(assemblyPath)
                : Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

            return Path.Combine(baseDir, configDir);
        }

        private static Configuration LoadDllConfig()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(location))
                location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            var map = new ExeConfigurationFileMap { ExeConfigFilename = location + ".config" };
            return ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        }

        private static string Get(string key, string fallback)
        {
            var value = _config?.AppSettings?.Settings[key]?.Value;
            return !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }
    }
}