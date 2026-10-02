using System;
using System.IO;
using System.Text.Json;

namespace RiskGame.Services
{
    /// <summary>
    /// Obtiene la cadena de conexión de la base de datos sin almacenarla en el código fuente.
    /// </summary>
    public static class DatabaseConfig
    {
        /// <summary>
        /// Nombre de la variable de entorno que puede contener la cadena de conexión.
        /// </summary>
        private const string EnvironmentVariableName = "RISKGAME_DB_CONNECTION";

        /// <summary>
        /// Nombre del archivo local que puede contener la cadena de conexión.
        /// </summary>
        private const string SettingsFileName = "appsettings.local.json";

        /// <summary>
        /// Resuelve la cadena de conexión consultando, en orden, la variable de entorno
        /// <c>RISKGAME_DB_CONNECTION</c> y luego el archivo <c>appsettings.local.json</c> con la
        /// clave <c>ConnectionStrings:RiskGameDB</c>.
        /// </summary>
        /// <returns>La cadena de conexión configurada para el servidor de SQL Server.</returns>
        /// <exception cref="InvalidOperationException">
        /// Se lanza cuando la cadena de conexión no está definida ni en la variable de entorno
        /// ni en el archivo local.
        /// </exception>
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
                if (document.RootElement.TryGetProperty("ConnectionStrings", out JsonElement section) &&
                    section.TryGetProperty("RiskGameDB", out JsonElement value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
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