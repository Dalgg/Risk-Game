using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Models;
using RiskGame.Server.Services;
using DatabaseLobby = RiskGame.Models.Lobby;
using DatabaseMatch = RiskGame.Models.Match;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class PersistedAbandonmentBuilder : IDisposable
    {
        public const int WinnerId = 41;
        public const int OpponentId = 43;
        private const int GuestWinnerId = -41;
        private const int MatchId = 601;
        private const int LobbyId = 801;
        private const int TurnSeconds = 60;
        private const string ConnectionString = "Data Source=:memory:";
        private const string LobbyCode = "ABAND1";
        private const string StandardRules = "Standard";
        private const string InitialState = "{}";
        private const string TestPasswordHash = "test-password-hash";
        private readonly SqliteConnection connection = new SqliteConnection(ConnectionString);
        private readonly DbContextOptions<RiskGameContext> options;

        public PersistedAbandonmentBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext context = CreateContext();
            context.Database.EnsureCreated();
            context.Users.Add(CreateUser(WinnerId, "winner"));
            context.Users.Add(CreateUser(OpponentId, "opponent"));
            context.Lobbies.Add(new DatabaseLobby
            {
                IdLobby = LobbyId, LobbyCode = LobbyCode, RulesVariant = StandardRules,
                TurnDuration = TurnSeconds, CreationDate = DateTime.UtcNow
            });
            context.Matches.Add(new DatabaseMatch
            {
                IdMatch = MatchId, IdLobby = LobbyId, StartDate = DateTime.UtcNow
            });
            context.MatchStates.Add(new MatchState
            {
                IdMatch = MatchId, StateData = InitialState, LastUpdated = DateTime.UtcNow
            });
            context.SaveChanges();
            Repository = new MatchResultsRepositoryService(CreateContext);
        }

        public MatchResultsRepositoryService Repository { get; }

        public RiskGameContext CreateContext()
        {
            return new RiskGameContext(options);
        }

        public ActiveMatch CreateActiveMatch(bool guestWinner = false)
        {
            int winnerId = WinnerId;
            if (guestWinner)
            {
                winnerId = GuestWinnerId;
            }
            LobbyDetails lobby = new LobbyDetails
            {
                LobbyId = LobbyId, Rules = new LobbyRules { TurnDurationSeconds = TurnSeconds }
            };
            lobby.Players.Add(new LobbyPlayer
            {
                Player = new PlayerSummary { UserId = winnerId, IsGuest = guestWinner }
            });
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = OpponentId } });
            ActiveMatch match = new ActiveMatch(MatchId, lobby, new ManualTimeProvider());
            match.UpdateConnection(winnerId, true);
            match.UpdateConnection(OpponentId, true);
            return match;
        }

        public ActiveMatch CreateAbandonment(bool guestWinner = false)
        {
            ActiveMatch match = CreateActiveMatch(guestWinner);
            match.AbandonParticipant(OpponentId);
            return match;
        }

        public ActiveMatch CreateCancellation()
        {
            ActiveMatch match = CreateActiveMatch();
            match.UpdateConnection(WinnerId, false);
            match.AbandonParticipant(OpponentId);
            match.AbandonParticipant(WinnerId);
            return match;
        }

        public ActiveMatch CreateVictory()
        {
            return new CompletedMatchBuilder().WithMatchId(MatchId).Build();
        }

        public DatabaseMatch ReadMatch()
        {
            using RiskGameContext context = CreateContext();
            return context.Matches.AsNoTracking().Single(match => match.IdMatch == MatchId);
        }

        public IReadOnlyList<MatchResult> ReadResults()
        {
            using RiskGameContext context = CreateContext();
            return context.MatchResults.AsNoTracking().Where(result => result.IdMatch == MatchId).ToList();
        }

        public int CountStates()
        {
            using RiskGameContext context = CreateContext();
            return context.MatchStates.Count(state => state.IdMatch == MatchId);
        }

        private static User CreateUser(int userId, string username)
        {
            return new User
            {
                IdUser = userId, Username = username, Nickname = username,
                Email = username + "@example.com", PasswordHash = TestPasswordHash,
                RegistrationDate = DateTime.UtcNow
            };
        }

        public void Dispose()
        {
            connection.Dispose();
        }
    }
}
