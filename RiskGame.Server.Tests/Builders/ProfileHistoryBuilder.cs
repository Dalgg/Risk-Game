using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Profile;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ProfileHistoryBuilder : IDisposable
    {
        public const int UserId = 41;
        public const int OtherUserId = 43;
        public const int RecentLimit = 5;
        public const int FirstMatchId = 100;
        public const int MostRecentMatchId = 107;
        public const int ForeignMatchId = 998;
        public const int UnfinishedMatchId = 999;
        public const int OlderHighIdentifier = 300;
        public const int NewerLowIdentifier = 101;
        public const int TieFirstId = 201;
        public const int TieSecondId = 202;
        public const int WinningPosition = 1;
        public const int LosingPosition = 2;
        public const int ClaimedColumns = 3;
        private const int LobbyId = 10;
        private const int FirstSampleIndex = 1;
        private const int LastSampleIndex = 7;
        private const int ForeignDaysOffset = 20;
        private const int NewerDaysOffset = 2;
        private const int MatchWithoutResultId = 700;
        private const int TurnSeconds = 60;
        private const int MatchDurationMinutes = 10;
        private const int Year = 2026;
        private const int Month = 11;
        private const int Day = 20;
        private const int Hour = 12;
        private const int NoMinutes = 0;
        private const int NoSeconds = 0;
        private const string InMemoryConnectionString = "Data Source=:memory:";
        private readonly SqliteConnection connection = new SqliteConnection(InMemoryConnectionString);
        private readonly DbContextOptions<RiskGameContext> options;
        private readonly ProfileSessionRegistry sessions = new ProfileSessionRegistry();

        public ProfileHistoryBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext context = CreateContext();
            context.Database.EnsureCreated();
            context.Users.Add(CreateUser(UserId, "history.user"));
            context.Users.Add(CreateUser(OtherUserId, "other.user"));
            context.Lobbies.Add(new Lobby
            {
                IdLobby = LobbyId, LobbyCode = "HISTORY", RulesVariant = "Standard",
                TurnDuration = TurnSeconds, CreationDate = BaseTime
            });
            context.SaveChanges();
            Repository = new ProfileRepositoryService(CreateContext);
            Service = new ProfileGrpcService(Repository, sessions);
            Token = sessions.Register(UserId);
        }

        public static DateTime BaseTime => new DateTime(Year, Month, Day, Hour, NoMinutes, NoSeconds, DateTimeKind.Utc);

        public ProfileRepositoryService Repository { get; }

        public ProfileGrpcService Service { get; }

        public string Token { get; }

        public RiskGameContext CreateContext()
        {
            return new RiskGameContext(options);
        }

        public void SeedRecentMatches()
        {
            for (int sampleIndex = FirstSampleIndex; sampleIndex <= LastSampleIndex; sampleIndex++)
            {
                AddResult(new HistorySeed
                {
                    MatchId = FirstMatchId + sampleIndex, UserId = UserId,
                    EndDate = BaseTime.AddMinutes(sampleIndex), FinalScore = ClaimedColumns,
                    FinalPosition = LosingPosition, EndReason = "Victory"
                });
            }
            AddResult(new HistorySeed
            {
                MatchId = ForeignMatchId, UserId = OtherUserId, EndDate = BaseTime.AddDays(ForeignDaysOffset),
                FinalScore = ClaimedColumns, FinalPosition = WinningPosition, EndReason = "Victory"
            });
            AddResult(new HistorySeed
            {
                MatchId = UnfinishedMatchId, UserId = UserId,
                EndDate = null,
                FinalScore = ClaimedColumns, FinalPosition = WinningPosition
            });
        }

        public void SeedOutOfOrderIdentifiers()
        {
            AddResult(new HistorySeed { MatchId = OlderHighIdentifier, UserId = UserId, EndDate = BaseTime });
            AddResult(new HistorySeed
            {
                MatchId = NewerLowIdentifier, UserId = UserId, EndDate = BaseTime.AddDays(NewerDaysOffset)
            });
        }

        public void SeedTiedEndDates()
        {
            AddResult(new HistorySeed { MatchId = TieFirstId, UserId = UserId, EndDate = BaseTime });
            AddResult(new HistorySeed { MatchId = TieSecondId, UserId = UserId, EndDate = BaseTime });
        }

        public void AddResult(HistorySeed seed)
        {
            using RiskGameContext context = CreateContext();
            context.Matches.Add(new RiskGame.Models.Match
            {
                IdMatch = seed.MatchId, IdLobby = LobbyId,
                StartDate = BaseTime.AddMinutes(-MatchDurationMinutes), EndDate = seed.EndDate,
                EndReason = seed.EndReason
            });
            context.MatchResults.Add(new MatchResult
            {
                IdMatch = seed.MatchId, IdUser = seed.UserId,
                FinalPosition = seed.FinalPosition, FinalScore = seed.FinalScore
            });
            context.SaveChanges();
        }

        public void SeedCancelledMatchWithoutResults()
        {
            using RiskGameContext context = CreateContext();
            context.Matches.Add(new RiskGame.Models.Match
            {
                IdMatch = MatchWithoutResultId, IdLobby = LobbyId,
                StartDate = BaseTime, EndDate = BaseTime.AddMinutes(MatchDurationMinutes), EndReason = "Cancelled"
            });
            context.SaveChanges();
        }

        public UserProfile ReadProfile()
        {
            return Repository.GetProfile(UserId);
        }

        public UserProfile ReadAuthenticatedProfile(int requestedUserId)
        {
            ProfileQuery request = new ProfileQuery { UserId = requestedUserId, SessionToken = Token };
            return Service.GetProfile(request, null!).GetAwaiter().GetResult();
        }

        public void ExpireSession()
        {
            sessions.RemoveSession(Token);
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        private static User CreateUser(int userId, string username)
        {
            return new User
            {
                IdUser = userId, Username = username, Nickname = username, Email = username + "@example.com",
                PasswordHash = "history-test-password", RegistrationDate = BaseTime
            };
        }

        public sealed class HistorySeed
        {
            public int MatchId { get; init; } = FirstMatchId;

            public int UserId { get; init; } = ProfileHistoryBuilder.UserId;

            public DateTime? EndDate { get; init; } = BaseTime;

            public int? FinalScore { get; init; }

            public int? FinalPosition { get; init; }

            public string? EndReason { get; init; }
        }
    }
}
