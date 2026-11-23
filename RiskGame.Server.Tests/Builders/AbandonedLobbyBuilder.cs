using System;
using System.Threading;
using System.Threading.Tasks;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Server.Services;
using RiskGame.Services;
using DatabaseLobby = RiskGame.Models.Lobby;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class AbandonedLobbyBuilder : IDisposable
    {
        public const int HostId = 41;
        public const int RemainingId = 43;
        public const int ThirdId = 45;
        public const int NewPlayerId = 47;
        public const int OriginalLobbyId = 18;
        public const int NewLobbyId = 19;
        public const int MatchId = 301;
        public const int DifferentMatchId = 302;
        private const int TurnSeconds = 60;
        private const string OriginalCode = "RULES1";
        private const string NewCode = "RULES2";
        private readonly ProfileSessionRegistry sessions = new ProfileSessionRegistry();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Task subscription = Task.CompletedTask;

        public AbandonedLobbyBuilder()
        {
            Matches = new MatchSessionManager(sessions, new RecordingMatchRepository(), new ManualTimeProvider());
            Lobbies = new LobbySessionManager(new LobbyRepository(), sessions, Matches);
            HostToken = sessions.Register(HostId);
            RemainingToken = sessions.Register(RemainingId);
            ThirdToken = sessions.Register(ThirdId);
            Lobbies.CreateLobby(Rules(), Player(HostId));
            Lobbies.JoinLobby(OriginalCode, Player(RemainingId));
            Lobbies.JoinLobby(OriginalCode, Player(ThirdId));
            Lobbies.StartMatch(HostId);
        }

        public LobbySessionManager Lobbies { get; }

        public MatchSessionManager Matches { get; }

        public string HostToken { get; }

        public string RemainingToken { get; }

        public string ThirdToken { get; }

        public AbandonedMatchPlayer HostAbandonment()
        {
            return new AbandonedMatchPlayer(MatchId, OriginalLobbyId, HostId);
        }

        public LobbyDetails Snapshot(int userId = RemainingId)
        {
            return Lobbies.RegisterSubscriber(userId, new LobbySettingsBuilder.SettingsStream());
        }

        public LobbyDetails MoveHostToNewLobby()
        {
            return Lobbies.CreateLobby(Rules(), Player(HostId));
        }

        public void FinishByAbandonment()
        {
            TimedMatchBuilder.SnapshotStream stream = new TimedMatchBuilder.SnapshotStream();
            MatchSubscription survivor = Request(RemainingId, RemainingToken);
            subscription = Task.Run(() => Matches.SubscribeMatchEvents(survivor, stream, cancellation.Token));
            stream.Read();
            Matches.LeaveMatch(Request(HostId, HostToken));
            Matches.LeaveMatch(Request(ThirdId, ThirdToken));
        }

        public void DrainCleanup()
        {
            foreach (AbandonedMatchPlayer abandoned in Matches.TakeAbandonedParticipants())
            {
                Lobbies.RemoveAbandonedParticipant(abandoned);
            }
        }

        public LobbyDetails ReturnSurvivorAndJoinNewPlayer()
        {
            Lobbies.ReturnToLobby(new ReturnToLobbyRequest
            {
                MatchId = MatchId, UserId = RemainingId, SessionToken = RemainingToken
            });
            return Lobbies.JoinLobby(OriginalCode, Player(NewPlayerId));
        }

        private static MatchSubscription Request(int userId, string token)
        {
            return new MatchSubscription { MatchId = MatchId, UserId = userId, SessionToken = token };
        }

        private static LobbyRules Rules()
        {
            return new LobbyRules { TurnDurationSeconds = TurnSeconds };
        }

        private static PlayerSummary Player(int userId)
        {
            return new PlayerSummary { UserId = userId, Nickname = "Player" };
        }

        public void Dispose()
        {
            cancellation.Cancel();
            subscription.GetAwaiter().GetResult();
            cancellation.Dispose();
        }

        private sealed class LobbyRepository : LobbyRepositoryService
        {
            private const int FirstLobby = 1;
            private const string StandardRules = "Standard";
            private int createdLobbies;

            public override DatabaseLobby CreateLobbyInDatabase(string rulesVariantName, int turnDurationSeconds)
            {
                createdLobbies++;
                string code = OriginalCode;
                if (createdLobbies > FirstLobby)
                {
                    code = NewCode;
                }
                return GetActiveLobbyByCode(code);
            }

            public override DatabaseLobby GetActiveLobbyByCode(string lobbyCode)
            {
                int lobbyId = OriginalLobbyId;
                if (lobbyCode == NewCode)
                {
                    lobbyId = NewLobbyId;
                }
                return new DatabaseLobby
                {
                    IdLobby = lobbyId, LobbyCode = lobbyCode, RulesVariant = StandardRules,
                    TurnDuration = TurnSeconds
                };
            }

            public override int CreateMatchInDatabase(int lobbyId)
            {
                return MatchId;
            }
        }
    }
}
