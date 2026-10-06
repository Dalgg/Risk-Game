using System;
using System.IO;
using System.Text.Json;

namespace RiskGame.Services
{
    public static class DatabaseConfig
    {
        private const string EnvironmentVariableName = "RISKGAME_DB_CONNECTION";
        private const string SettingsFileName = "appsettings.local.json";

        public static string GetConnectionString()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }

            foreach (string directory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                string path = Path.Combine(directory, SettingsFileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("ConnectionStrings", out JsonElement section) && section
                    .TryGetProperty("RiskGameDB", out JsonElement value) && value.ValueKind == JsonValueKind.String 
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }

            throw new InvalidOperationException(
                $"No se encontró la cadena de conexión. Define la variable de entorno {EnvironmentVariableName} " +
                $"o crea el archivo {SettingsFileName} a partir de appsettings.example.json.");
        }
    }
}