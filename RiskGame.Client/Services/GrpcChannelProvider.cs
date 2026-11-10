using System;
using System.IO;
using System.Text.Json;
using Grpc.Net.Client;

namespace RiskGame.Services
{
    public static class GrpcChannelProvider
    {
        private const string DefaultServerAddress = "http://localhost:5050";
        private const string SettingsFileName = "appsettings.local.json";
        private const string ServerUrlPropertyName = "GrpcServerUrl";
        private static GrpcChannel? cachedChannel;

        public static GrpcChannel GetChannel()
        {
            if (cachedChannel == null)
            {
                string serverAddress = ResolveServerAddress();
                cachedChannel = GrpcChannel.ForAddress(serverAddress);
            }

            return cachedChannel;
        }

        private static string ResolveServerAddress()
        {
            string[] candidateDirectories = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string candidateDirectory in candidateDirectories)
            {
                string settingsFilePath = Path.Combine(candidateDirectory, SettingsFileName);
                if (!File.Exists(settingsFilePath))
                {
                    continue;
                }

                try
                {
                    using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
                    string? configuredAddress = ExtractServerUrl(jsonDocument.RootElement);
                    if (!string.IsNullOrWhiteSpace(configuredAddress))
                    {
                        return configuredAddress;
                    }
                }
                catch (JsonException jsonException)
                {
                    JsonLogger.Error(jsonException, "Failed to parse local configuration file {0}.", settingsFilePath);
                    return DefaultServerAddress;
                }
            }

            return DefaultServerAddress;
        }

        private static string? ExtractServerUrl(JsonElement rootElement)
        {
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            foreach (JsonProperty jsonProperty in rootElement.EnumerateObject())
            {
                if (string.Equals(jsonProperty.Name, ServerUrlPropertyName, StringComparison.Ordinal)
                    && jsonProperty.Value.ValueKind == JsonValueKind.String)
                {
                    return jsonProperty.Value.GetString();
                }
            }

            return string.Empty;
        }
    }
}
