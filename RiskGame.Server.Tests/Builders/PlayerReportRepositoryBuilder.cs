using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class PlayerReportRepositoryBuilder : IDisposable
    {
        public const int FirstReporterId = 41;
        public const int SecondReporterId = 43;
        public const int ThirdReporterId = 47;
        public const int ReportedId = 49;
        public const int FirstLobbyId = 18;
        public const int SecondLobbyId = 19;
        public const int ThirdLobbyId = 20;
        public const int UnknownUserId = 999;
        public const int UnknownLobbyId = 998;
        public const int GuestId = -41;
        public const int SuspensionHours = 24;
        public const int RequiredDistinctReporters = 3;
        public const int NewSuspensionOffsetHours = 25;
        private const int InitialYear = 2026;
        private const int InitialMonth = 11;
        private const int InitialDay = 26;
        private const int InitialHour = 12;
        private const int InitialMinute = 0;
        private const int InitialSecond = 0;
        private const int TurnSeconds = 60;
        private const string ConnectionString = "Data Source=:memory:";
        private readonly SqliteConnection connection = new SqliteConnection(ConnectionString);
        private readonly DbContextOptions<RiskGameContext> options;

        public PlayerReportRepositoryBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext databaseContext = CreateContext();
            databaseContext.Database.EnsureCreated();
            databaseContext.Users.Add(CreateUser(FirstReporterId));
            databaseContext.Users.Add(CreateUser(SecondReporterId));
            databaseContext.Users.Add(CreateUser(ThirdReporterId));
            databaseContext.Users.Add(CreateUser(ReportedId));
            databaseContext.Lobbies.Add(CreateLobby(FirstLobbyId));
            databaseContext.Lobbies.Add(CreateLobby(SecondLobbyId));
            databaseContext.Lobbies.Add(CreateLobby(ThirdLobbyId));
            databaseContext.SaveChanges();
            Repository = new PlayerReportRepositoryService(CreateContext);
        }

        public static DateTime BaseTime => new DateTime(InitialYear, InitialMonth, InitialDay,
            InitialHour, InitialMinute, InitialSecond, DateTimeKind.Utc);

        public PlayerReportRepositoryService Repository { get; }

        public bool FailSuspensionSave { get; set; }

        public RiskGameContext CreateContext()
        {
            RiskGameContext databaseContext;
            if (FailSuspensionSave)
            {
                databaseContext = new FailingSuspensionContext(options);
            }
            else
            {
                databaseContext = new RiskGameContext(options);
            }
            return databaseContext;
        }

        public PlayerReportSubmission Submission(int reporterId, int lobbyId)
        {
            return new PlayerReportSubmission
            {
                ReporterId = reporterId, ReportedId = ReportedId, LobbyId = lobbyId,
                ReportDateUtc = BaseTime, BannedUntilUtc = BaseTime.AddHours(SuspensionHours),
                RequiredDistinctReporters = RequiredDistinctReporters
            };
        }

        public PlayerReportResult Report(int reporterId, int lobbyId)
        {
            return Repository.RecordReport(Submission(reporterId, lobbyId));
        }

        public PlayerReportResult TriggerSuspension()
        {
            Report(FirstReporterId, FirstLobbyId);
            Report(SecondReporterId, FirstLobbyId);
            return Report(ThirdReporterId, FirstLobbyId);
        }

        public int ReportCount()
        {
            using RiskGameContext databaseContext = CreateContext();
            return databaseContext.PlayerReports.Count();
        }

        public int PendingCount()
        {
            using RiskGameContext databaseContext = CreateContext();
            return databaseContext.PlayerReports.Count(report => !report.IsProcessed);
        }

        public DateTime? StoredBannedUntil()
        {
            using RiskGameContext databaseContext = CreateContext();
            return databaseContext.Users.Single(user => user.IdUser == ReportedId).BannedUntil;
        }

        public PlayerReport ReadFirstReport()
        {
            using RiskGameContext databaseContext = CreateContext();
            return databaseContext.PlayerReports.OrderBy(report => report.IdPlayerReport).First();
        }

        public PlayerReportResult ReportAfterSuspension()
        {
            DateTime later = BaseTime.AddHours(NewSuspensionOffsetHours);
            return Repository.RecordReport(new PlayerReportSubmission
            {
                ReporterId = FirstReporterId, ReportedId = ReportedId, LobbyId = SecondLobbyId,
                ReportDateUtc = later, BannedUntilUtc = later.AddHours(SuspensionHours),
                RequiredDistinctReporters = RequiredDistinctReporters
            });
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        private static User CreateUser(int userId)
        {
            return new User
            {
                IdUser = userId, Username = "report.user." + userId,
                Email = "report." + userId + "@test.invalid", Nickname = "Report " + userId,
                PasswordHash = "test-hash", RegistrationDate = BaseTime
            };
        }

        private static Lobby CreateLobby(int lobbyId)
        {
            return new Lobby
            {
                IdLobby = lobbyId, LobbyCode = "RPT" + lobbyId,
                RulesVariant = "Standard", TurnDuration = TurnSeconds, CreationDate = BaseTime
            };
        }

        private sealed class FailingSuspensionContext : RiskGameContext
        {
            public FailingSuspensionContext(DbContextOptions<RiskGameContext> options) : base(options)
            {
            }

            public override int SaveChanges()
            {
                bool suspensionChanged = ChangeTracker.Entries<User>().Any(entry =>
                    entry.State == EntityState.Modified && entry.Entity.BannedUntil.HasValue);
                if (suspensionChanged)
                {
                    throw new DbUpdateException("Simulated suspension persistence failure.");
                }
                return base.SaveChanges();
            }
        }
    }
}
