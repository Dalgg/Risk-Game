using System;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ReportFlowBuilder
    {
        public const int HostId = 41;
        public const int ReporterId = 43;
        public const int OutsiderId = 47;
        public const int GuestId = -49;
        public const int LobbyId = 18;
        public const int WrongLobbyId = 19;
        public const int NoReports = 0;
        public const int OneReport = 1;
        public const int SuspensionHours = 24;
        private const int TurnSeconds = 60;
        private const string LobbyCode = "RULES1";
        private const string Nickname = "Report player";

        public ReportFlowBuilder()
        {
            Time = new ManualTimeProvider();
            Registry = new ProfileSessionRegistry(Time);
            HostToken = Registry.Register(HostId);
            ReporterToken = Registry.Register(ReporterId);
            Repository = new RecordingReportRepository();
            Matches = new MatchSessionManager(Registry, new RecordingMatchRepository(), Time);
            Lobbies = new LobbySessionManager(new LobbySettingsBuilder.SettingsRepository(), Registry, Matches);
            Lobbies.CreateLobby(new LobbyRules { TurnDurationSeconds = TurnSeconds }, Player(HostId));
            Lobbies.JoinLobby(LobbyCode, Player(ReporterId));
            Manager = new ReportSessionManager(Lobbies, Repository, Time);
        }

        public ManualTimeProvider Time { get; }

        public ProfileSessionRegistry Registry { get; }

        public LobbySessionManager Lobbies { get; }

        public MatchSessionManager Matches { get; }

        public RecordingReportRepository Repository { get; }

        public ReportSessionManager Manager { get; }

        public string HostToken { get; }

        public string ReporterToken { get; }

        public PlayerReportCommand Command()
        {
            return CommandForPlayer(ReporterId, ReporterToken);
        }

        public PlayerReportCommand CommandForPlayer(int reporterId, string sessionToken)
        {
            return new PlayerReportCommand
            {
                UserId = reporterId,
                SessionToken = sessionToken,
                ReportedUserId = HostId,
                LobbyId = ReportFlowBuilder.LobbyId
            };
        }

        public PlayerReportCommand CommandForTarget(int targetId)
        {
            return new PlayerReportCommand
            {
                UserId = ReporterId,
                SessionToken = ReporterToken,
                ReportedUserId = targetId,
                LobbyId = ReportFlowBuilder.LobbyId
            };
        }

        public PlayerReportCommand CommandForLobby(int lobbyId)
        {
            return new PlayerReportCommand
            {
                UserId = ReporterId,
                SessionToken = ReporterToken,
                ReportedUserId = HostId,
                LobbyId = lobbyId
            };
        }

        public LobbyDetails Snapshot()
        {
            return Lobbies.RegisterSubscriber(ReporterId, new LobbySettingsBuilder.SettingsStream());
        }

        public void JoinGuest()
        {
            Lobbies.JoinLobby(LobbyCode, Player(GuestId));
        }

        public void SuspendReportedHost()
        {
            Repository.ShouldSuspend = true;
            Manager.ReportPlayer(Command());
        }

        private static PlayerSummary Player(int userId)
        {
            return new PlayerSummary { UserId = userId, Nickname = Nickname };
        }

        public sealed class RecordingReportRepository : PlayerReportRepositoryService
        {
            public RecordingReportRepository() : base(CreateUnavailableContext)
            {
            }

            public int Calls { get; private set; }

            public bool ShouldSuspend { get; set; }

            public bool IsDuplicate { get; set; }

            public bool FailSave { get; set; }

            public PlayerReportSubmission LastSubmission { get; private set; } = new PlayerReportSubmission();

            public override PlayerReportResult RecordReport(PlayerReportSubmission submission)
            {
                Calls++;
                LastSubmission = submission;
                if (FailSave)
                {
                    throw new DataOperationException("The simulated report database is unavailable.");
                }
                DateTime? bannedUntil = null;
                if (ShouldSuspend)
                {
                    bannedUntil = submission.BannedUntilUtc;
                }
                return new PlayerReportResult { IsDuplicate = IsDuplicate, BannedUntil = bannedUntil };
            }

            private static RiskGameContext CreateUnavailableContext()
            {
                throw new InvalidOperationException("The recording repository must not access a database.");
            }
        }
    }
}
