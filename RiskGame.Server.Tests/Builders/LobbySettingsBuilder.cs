using System;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using Lobby = RiskGame.Models.Lobby;
using RiskGame.Server.Services;
using RiskGame.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class LobbySettingsBuilder
    {
        public const int HostId = 41;
        public const int OtherId = 43;
        public const int DefaultDuration = 60;
        public const int LongDuration = 90;
        private const int LobbyId = 18;
        private const int MatchId = 301;
        private const string LobbyCode = "RULES1";
        private readonly ProfileSessionRegistry registry = new ProfileSessionRegistry();

        public LobbySettingsBuilder()
        {
            Repository = new SettingsRepository();
            Manager = new LobbySessionManager(Repository, registry,
                new MatchSessionManager(registry, new RecordingMatchRepository()));
            Token = registry.Register(HostId);
            Manager.CreateLobby(new LobbyRules { TurnDurationSeconds = DefaultDuration }, Player(HostId));
            Manager.JoinLobby(LobbyCode, Player(OtherId));
        }

        public LobbySessionManager Manager { get; }

        public SettingsRepository Repository { get; }

        public string Token { get; }

        public LobbyRules UpdatedRules()
        {
            return new LobbyRules { RulesVariant = RulesVariant.Fast, TurnDurationSeconds = LongDuration };
        }

        public LobbyDetails UpdateHostRules()
        {
            return Manager.UpdateRules(HostId, Token, UpdatedRules());
        }

        public LobbyDetails Snapshot()
        {
            return Manager.RegisterSubscriber(HostId, new SettingsStream());
        }

        public void ExpireSession()
        {
            registry.RemoveSession(Token);
        }

        private static PlayerSummary Player(int userId)
        {
            return new PlayerSummary { UserId = userId, Nickname = "Player" };
        }

        public sealed class SettingsStream : IServerStreamWriter<LobbyEvent>
        {
            private const int WaitSeconds = 5;
            private readonly TaskCompletionSource<LobbyEvent> received = new TaskCompletionSource<LobbyEvent>();

            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(LobbyEvent message)
            {
                received.TrySetResult(message);
                return Task.CompletedTask;
            }

            public LobbyEvent Read()
            {
                return received.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds)).GetAwaiter().GetResult();
            }
        }

        public sealed class SettingsRepository : LobbyRepositoryService
        {
            public string SavedVariant { get; private set; } = string.Empty;

            public int SavedDuration { get; private set; }

            public bool FailSave { get; set; }

            public override Lobby CreateLobbyInDatabase(string rulesVariantName, int turnDurationSeconds)
            {
                return GetActiveLobbyByCode(LobbyCode);
            }

            public override Lobby GetActiveLobbyByCode(string lobbyCode)
            {
                return new Lobby
                {
                    IdLobby = LobbyId, LobbyCode = LobbyCode,
                    RulesVariant = "Standard", TurnDuration = DefaultDuration
                };
            }

            public override void UpdateRules(int lobbyId, string rulesVariantName, int turnDurationSeconds)
            {
                if (FailSave)
                {
                    throw new DataOperationException("Test persistence failure.");
                }
                SavedVariant = rulesVariantName;
                SavedDuration = turnDurationSeconds;
            }

            public override int CreateMatchInDatabase(int lobbyId)
            {
                return MatchId;
            }
        }
    }
}
