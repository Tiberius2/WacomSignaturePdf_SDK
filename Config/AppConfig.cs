using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace WacomSignaturePdf.Config
{
    // Centralized configuration management for the application.
    public static class AppConfig
    {
        private static readonly Configuration _config = LoadDllConfig();

        public static readonly string WorkingRoot = ResolveWorkingRoot();
        public static readonly string TemplatesDir = ResolveTemplatesDir();

        private static string ResolveTemplatesDir()
        {
            string td = Get("TemplatesDir", "Document Templates");
            if (string.IsNullOrWhiteSpace(td)) return null;

            // Assembly.Location returns empty when embedded via Costura — fall back to process path
            string asmLocation = Assembly.GetExecutingAssembly().Location;
            string baseDir = !string.IsNullOrWhiteSpace(asmLocation)
                ? Path.GetDirectoryName(asmLocation)
                : Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

            return Path.Combine(baseDir, td);
        }
        private static string ResolveWorkingRoot()
        {
            string envPath = Environment.GetEnvironmentVariable("RecruitmentDocsPath");
            if (!string.IsNullOrWhiteSpace(envPath))
                return envPath;

            string configVal = Get("WorkingRoot", null);
            if (!string.IsNullOrWhiteSpace(configVal))
                return configVal;

            return null; // handled gracefully in MainForm
        }

        // Loads configuration from a .config file located next to the DLL.
        //private static Configuration LoadDllConfig()
        //{
        //    string dllPath = Assembly.GetExecutingAssembly().Location;
        //    var map = new ExeConfigurationFileMap { ExeConfigFilename = dllPath + ".config" };
        //    return ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        //}

        private static Configuration LoadDllConfig()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(location))
                location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            var map = new ExeConfigurationFileMap { ExeConfigFilename = location + ".config" };
            return ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        }


        // Retrieves a configuration value by key, returning a fallback if not found or empty.
        private static string Get(string key, string fallback)
        {
            var val = _config?.AppSettings?.Settings[key]?.Value;
            return !string.IsNullOrWhiteSpace(val) ? val : fallback;
        }
    }
}