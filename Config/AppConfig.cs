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

        public static readonly string WorkingRoot = Get("WorkingRoot",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SignatureWork"));

        public static readonly string TemplatesDir = Get("TemplatesDir", "Document Templates") is string td
            ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), td)
            : null;


        // Loads configuration from a .config file located next to the DLL.
        private static Configuration LoadDllConfig()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            var map = new ExeConfigurationFileMap { ExeConfigFilename = dllPath + ".config" };
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