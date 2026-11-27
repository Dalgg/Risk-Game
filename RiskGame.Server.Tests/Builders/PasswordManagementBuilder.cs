using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Profile;
using RiskGame.Models;
using RiskGame.Server.Services;
using RiskGame.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class PasswordManagementBuilder : IDisposable
    {
        public const int UserId = 71;
        public const int OtherUserId = 73;
        public const int GuestId = -71;
        public const string Username = "password.tester";
        public const string Email = "password.tester@example.com";
        public const string InitialPassword = "OldPassword1!";
        public const string ReplacementPassword = "NewPassword2@";
        public const string WeakPassword = "weak";
        public const string InvalidCode = "000000";
        public const int ResetExpiryMinutes = 5;
        public const int ResendCooldownSeconds = 60;
        public const int BeforeCooldownSeconds = 30;
        public const int MaximumFailedAttempts = 5;
        private const string InMemoryConnectionString = "Data Source=:memory:";
        private readonly SqliteConnection connection = new SqliteConnection(InMemoryConnectionString);
        private readonly DbContextOptions<RiskGameContext> options;
        private readonly ProfileSessionRegistry sessions = new ProfileSessionRegistry();

        public PasswordManagementBuilder()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<RiskGameContext>().UseSqlite(connection).Options;
            using RiskGameContext context = CreateContext();
            context.Database.EnsureCreated();
            context.Users.Add(new User
            {
                IdUser = UserId,
                Username = Username,
                Nickname = Username,
                Email = Email,
                PasswordHash = PasswordHasher.Hash(InitialPassword),
                RegistrationDate = DateTime.UtcNow,
                AvatarReference = ProfileCustomizationRules.DefaultAvatarReference
            });
            context.SaveChanges();
            Time = new ManualTimeProvider();
            EmailDelivery = new RecordingEmailDeliveryService();
            AuthRepository = new AuthRepositoryService(CreateContext);
            ProfileRepository = new ProfileRepositoryService(CreateContext);
            ProfileService = new ProfileGrpcService(ProfileRepository, sessions);
            ResetManager = new PasswordResetManager(AuthRepository, EmailDelivery, Time);
            Token = sessions.Register(UserId);
            GuestToken = sessions.Register(GuestId);
        }

        public ManualTimeProvider Time { get; }

        public RecordingEmailDeliveryService EmailDelivery { get; }

        public AuthRepositoryService AuthRepository { get; }

        public ProfileRepositoryService ProfileRepository { get; }

        public ProfileGrpcService ProfileService { get; }

        public PasswordResetManager ResetManager { get; }

        public string Token { get; }

        public string GuestToken { get; }

        public RiskGameContext CreateContext()
        {
            return new RiskGameContext(options);
        }

        public void UpdatePasswordViaGrpc(int userId, string sessionToken, string currentPassword, string newPassword)
        {
            UpdatePasswordRequest request = new UpdatePasswordRequest
            {
                UserId = userId,
                SessionToken = sessionToken,
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            };
            ProfileService.UpdatePassword(request, null!).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        public sealed class RecordingEmailDeliveryService : IEmailDeliveryService
        {
            private readonly List<string> deliveredResetCodes = new List<string>();
            private readonly List<string> deliveredRecipients = new List<string>();

            public int SentResetCount => deliveredResetCodes.Count;

            public string LatestResetCode => deliveredResetCodes[deliveredResetCodes.Count - 1];

            public string LatestRecipient => deliveredRecipients[deliveredRecipients.Count - 1];

            public void SendVerification(string recipientEmail, string verificationCode)
            {
                deliveredRecipients.Add(recipientEmail);
                deliveredResetCodes.Add(verificationCode);
            }

            public void SendPasswordReset(string recipientEmail, string resetCode)
            {
                deliveredRecipients.Add(recipientEmail);
                deliveredResetCodes.Add(resetCode);
            }
        }
    }
}
