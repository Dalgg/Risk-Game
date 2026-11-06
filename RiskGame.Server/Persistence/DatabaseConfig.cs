using System;
using System.IO;
using System.Text.Json;

namespace RiskGame.Services
{
    public static class DatabaseConfig
    {
        private const string EnvironmentVariableName = "RISKGAME_DB_CONNECTION";
        private const string SettingsFileName = "appsettings.local.json";
        private const string ConnectionStringsSectionName = "ConnectionStrings";
        private const string DatabaseConnectionKeyName = "RiskGameDB";

        public static string GetConnectionString()
        {
            string? connectionFromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(connectionFromEnvironment))
            {
                return connectionFromEnvironment;
            }

            string[] candidateDirectories = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string candidateDirectory in candidateDirectories)
            {
                string settingsFilePath = Path.Combine(candidateDirectory, SettingsFileName);
                if (!File.Exists(settingsFilePath))
                {
                    continue;
                }

                using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
                string? configuredConnectionString = ExtractConnectionString(jsonDocument.RootElement);
                if (!string.IsNullOrWhiteSpace(configuredConnectionString))
                {
                    return configuredConnectionString;
                }
            }

            throw new InvalidOperationException(
                $"No se encontró la cadena de conexión. Define la variable de entorno {EnvironmentVariableName} " +
                $"o crea el archivo {SettingsFileName} a partir de appsettings.example.json.");
        }

        private static string? ExtractConnectionString(JsonElement rootElement)
        {
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty rootProperty in rootElement.EnumerateObject())
            {
                if (string.Equals(rootProperty.Name, ConnectionStringsSectionName, StringComparison.Ordinal)
                    && rootProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty connectionProperty in rootProperty.Value.EnumerateObject())
                    {
                        if (string.Equals(connectionProperty.Name, DatabaseConnectionKeyName, StringComparison.Ordinal)
                            && connectionProperty.Value.ValueKind == JsonValueKind.String)
                        {
                            return connectionProperty.Value.GetString();
                        }
                    }
                }
            }

            return null;
        }
    }
}