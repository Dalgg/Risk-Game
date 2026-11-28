using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Friend;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Invitation;
using RiskGame.Contracts.Lobby;
using RiskGame.Models;
using RiskGame.Server.Services;
using Lobby = RiskGame.Models.Lobby;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class InboxNotificationBuilder : IDisposable
    {
        public const int RecipientId = 41;
        public const int OtherUserId = 43;
        public const int SenderId = 47;
        public const int UnknownRequestId = 999;
        public const int PendingInvitationId = 31;
        public const long NoChanges = 0;
        public const long OneChange = 1;
        public const long TwoChanges = 2;
        public const string RecipientUsername = "inbox.recipient";
        public const string MissingUsername = "inbox.missing";
        private const string ConnectionString = "Data Source=:memory:";
        private const string SenderUsername = "inbox.sender";
        private const string OtherUsername = "inbox.other";
        private const string PasswordHash = "inbox-test-hash";
        private const string EmailSuffix = "@test.invalid";
        private const int LobbyId = 18;
        private const int TurnSeconds = 60;
        private const int InvitationMinutes = 30;
        private const string LobbyCode = "RULES1";
        private const string StandardRules = "Standard";
        private const string PendingStatus = "pending";
        private const string InvitationCode = "INBOX-CHECK";
        private readonly SqliteConnection connection = new SqliteConnection(ConnectionString);
        private readonly DbContextOptions<RiskGameContext> options;

        public InboxNotificationBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext databaseContext = CreateContext();
            databaseContext.Database.EnsureCreated();
            databaseContext.Users.Add(CreateUser(RecipientId, RecipientUsername));
            databaseContext.Users.Add(CreateUser(SenderId, SenderUsername));
            databaseContext.Users.Add(CreateUser(OtherUserId, OtherUsername));
            databaseContext.SaveChanges();
            Registry = new ProfileSessionRegistry();
            RecipientToken = Registry.Register(RecipientId);
            SenderToken = Registry.Register(SenderId);
            OtherToken = Registry.Register(OtherUserId);
            Repository = new FriendRepositoryService(CreateContext);
            Manager = new FriendSessionManager(Repository, Registry);
            Lobbies = new LobbySessionManager(new LobbySettingsBuilder.SettingsRepository(), Registry,
                new MatchSessionManager(Registry, new RecordingMatchRepository()));
            InvitationRepository = new InvitationRepositoryService(CreateContext);
            Invitations = new InvitationSessionManager(InvitationRepository, Lobbies, Registry);
        }

        public ProfileSessionRegistry Registry { get; }

        public FriendRepositoryService Repository { get; }

        public FriendSessionManager Manager { get; }

        public LobbySessionManager Lobbies { get; }

        public InvitationRepositoryService InvitationRepository { get; }

        public InvitationSessionManager Invitations { get; }

        public string RecipientToken { get; }

        public string SenderToken { get; }

        public string OtherToken { get; }

        public RiskGameContext CreateContext()
        {
            return new RiskGameContext(options);
        }

        public FriendRequest Request(string targetUsername = RecipientUsername)
        {
            return new FriendRequest
            {
                UserId = SenderId, SessionToken = SenderToken, TargetUsername = targetUsername
            };
        }

        public void SendRequest()
        {
            Manager.SendFriendRequest(Request());
        }

        public void Respond(bool accept)
        {
            Manager.RespondToRequest(new FriendRequestReply
            {
                UserId = RecipientId,
                SessionToken = RecipientToken,
                RequestId = FirstRequestId(),
                Accept = accept
            });
        }

        public FriendRequestReply InvalidReply()
        {
            return new FriendRequestReply
            {
                UserId = RecipientId, SessionToken = RecipientToken, RequestId = UnknownRequestId
            };
        }

        public FriendRequestReply CrossedReply()
        {
            return new FriendRequestReply
            {
                UserId = OtherUserId, SessionToken = RecipientToken, RequestId = FirstRequestId()
            };
        }

        public void SeedInvitation()
        {
            using RiskGameContext databaseContext = CreateContext();
            DateTime sentUtc = DateTime.UtcNow;
            databaseContext.Lobbies.Add(new Lobby
            {
                IdLobby = LobbyId, LobbyCode = LobbyCode, RulesVariant = StandardRules,
                TurnDuration = TurnSeconds, CreationDate = sentUtc
            });
            databaseContext.LobbyInvitations.Add(new LobbyInvitation
            {
                IdInvitation = PendingInvitationId, IdLobby = LobbyId,
                IdSender = SenderId, IdRecipient = RecipientId,
                InvitationCode = InvitationCode, Status = PendingStatus,
                SentDate = sentUtc, ExpirationDate = sentUtc.AddMinutes(InvitationMinutes)
            });
            databaseContext.SaveChanges();
        }

        public void OpenInvitedLobby()
        {
            Lobbies.CreateLobby(new LobbyRules { TurnDurationSeconds = TurnSeconds }, new PlayerSummary
            {
                UserId = SenderId, Nickname = SenderUsername
            });
        }

        public InvitationAction Action(int invitationId = PendingInvitationId)
        {
            return new InvitationAction
            {
                UserId = RecipientId, SessionToken = RecipientToken, InvitationId = invitationId
            };
        }

        private int FirstRequestId()
        {
            using RiskGameContext databaseContext = CreateContext();
            return databaseContext.Friendships.Single().IdFriendship;
        }

        private static User CreateUser(int userId, string username)
        {
            return new User
            {
                IdUser = userId,
                Username = username,
                Nickname = username,
                Email = username + EmailSuffix,
                PasswordHash = PasswordHash,
                RegistrationDate = DateTime.UtcNow
            };
        }

        public void Dispose()
        {
            connection.Dispose();
        }
    }
}
