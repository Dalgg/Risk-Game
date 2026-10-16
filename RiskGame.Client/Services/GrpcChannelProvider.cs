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
        private static GrpcChannel? s_channel;

        public static GrpcChannel GetChannel()
        {
            if (s_channel == null)
            {
                string serverAddress = ResolveServerAddress();
                s_channel = GrpcChannel.ForAddress(serverAddress);
            }

            return s_channel;
        }

        private static string ResolveServerAddress()
        {
            foreach (string directory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                string path = Path.Combine(directory, SettingsFileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                    if (document.RootElement.TryGetProperty("GrpcServerUrl", out JsonElement urlElement)
                        && urlElement.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(urlElement.GetString()))
                    {
                        return urlElement.GetString()!;
                    }
                }
                catch (JsonException)
                {
                    return DefaultServerAddress;
                }
            }

            return DefaultServerAddress;
        }
    }
}
