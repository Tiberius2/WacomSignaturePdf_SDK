using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace WacomSignaturePdf.Config
{
    public static class AppConfig
    {
        private static readonly Configuration _config = LoadDllConfig();

        public static readonly string WorkingRoot = ResolveWorkingRoot();
        public static readonly string TemplatesDir = ResolveTemplatesDir();
        public static readonly string FreeFormDocumentsPath = ResolveFreeFormDocumentsPath();

        private static string ResolveFreeFormDocumentsPath()
        {
            string env = Environment.GetEnvironmentVariable("FreeFormDocumentsPath");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            string config = Get("FreeFormDocumentsPath", null);
            return !string.IsNullOrWhiteSpace(config) ? config : null;
        }

        private static string ResolveWorkingRoot()
        {
            // Environment variable takes priority over app.config (allows per-machine GPO deployment)
            string env = Environment.GetEnvironmentVariable("RecruitmentDocsPath");
            if (!string.IsNullOrWhiteSpace(env)) return env;

            string config = Get("WorkingRoot", null);
            return !string.IsNullOrWhiteSpace(config) ? config : null;
        }
        private static string ResolveTemplatesDir()
        {
            string envPath = Environment.GetEnvironmentVariable("TemplateDocsPath");
            if (!string.IsNullOrWhiteSpace(envPath))
                return Path.Combine(envPath, "Sabloane Semnaturi Electronice");

            // Fallback la app.config pentru development local
            string td = Get("TemplatesDir", "Document Templates");
            if (string.IsNullOrWhiteSpace(td)) return null;

            string asmLocation = Assembly.GetExecutingAssembly().Location;
            string baseDir = !string.IsNullOrWhiteSpace(asmLocation)
                ? Path.GetDirectoryName(asmLocation)
                : Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

            return Path.Combine(baseDir, td);
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
            var val = _config?.AppSettings?.Settings[key]?.Value;
            return !string.IsNullOrWhiteSpace(val) ? val : fallback;
        }
    }
}