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

        private static string ResolveWorkingRoot()
        {
            // Primary: %RecruitmentDocsPath% env variable (set on each machine pointing to SharePoint sync)
            string envPath = Environment.GetEnvironmentVariable("RecruitmentDocsPath");
            if (!string.IsNullOrWhiteSpace(envPath))
                return Path.Combine(envPath, "DosarDocumenteRecrutare");

            // Fallback: App.config override (for dev/testing)
            string configVal = Get("WorkingRoot", null);
            if (!string.IsNullOrWhiteSpace(configVal))
                return configVal;

            throw new InvalidOperationException(
                "Variabila de mediu 'RecruitmentDocsPath' nu este configurata pe aceasta masina.\n" +
                "Contactati administratorul IT.");
        }

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