using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Profile;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ProfileCustomizationBuilder : IDisposable
    {
        public const int UserId = 41;
        public const int OtherUserId = 43;
        public const int GuestId = -41;
        public const string Username = "original.user";
        public const string OriginalNickname = "Original";
        public const string OriginalEmail = "original@example.com";
        public const string PasswordHash = "test-password-hash";
        private readonly SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        private readonly DbContextOptions<RiskGameContext> options;
        private readonly ProfileSessionRegistry sessions = new ProfileSessionRegistry();

        public ProfileCustomizationBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext context = CreateContext();
            context.Database.EnsureCreated();
            context.Users.Add(new User
            {
                IdUser = UserId, Username = Username, Nickname = OriginalNickname, Email = OriginalEmail,
                PasswordHash = PasswordHash, RegistrationDate = DateTime.UtcNow, AvatarReference = "default"
            });
            context.SaveChanges();
            Repository = new ProfileRepositoryService(CreateContext);
            Service = new ProfileGrpcService(Repository, sessions);
            Token = sessions.Register(UserId);
            GuestToken = sessions.Register(GuestId);
        }

        public ProfileRepositoryService Repository { get; }

        public ProfileGrpcService Service { get; }

        public string Token { get; }

        public string GuestToken { get; }

        public RiskGameContext CreateContext()
        {
            return new RiskGameContext(options);
        }

        public User ReadUser()
        {
            using RiskGameContext context = CreateContext();
            return context.Users.AsNoTracking().Single(user => user.IdUser == UserId);
        }

        public UserProfile UpdateNickname(int userId, string token, string nickname)
        {
            UpdateNicknameRequest request = new UpdateNicknameRequest
            {
                UserId = userId, SessionToken = token, Nickname = nickname
            };
            return Service.UpdateNickname(request, null!).GetAwaiter().GetResult();
        }

        public UserProfile UpdateAvatar(int userId, string token, string reference)
        {
            UpdateAvatarRequest request = new UpdateAvatarRequest
            {
                UserId = userId, SessionToken = token, AvatarReference = reference
            };
            return Service.UpdateAvatar(request, null!).GetAwaiter().GetResult();
        }

        public void ExpireSession()
        {
            sessions.RemoveSession(Token);
        }

        public void Dispose()
        {
            connection.Dispose();
        }
    }
}
