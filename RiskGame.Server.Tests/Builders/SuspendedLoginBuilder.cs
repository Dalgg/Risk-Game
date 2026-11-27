using System;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class SuspendedLoginBuilder
    {
        public const int SuspensionHours = 24;
        public const string Username = "suspended-login-player";
        public const string Password = "valid-test-password";
        public const string WrongPassword = "incorrect-test-password";
        private readonly AuthGrpcService service;

        public SuspendedLoginBuilder()
        {
            Flow = new ReportFlowBuilder();
            BannedUntilUtc = Flow.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;
            StoredUser = new User
            {
                IdUser = ReportFlowBuilder.OutsiderId,
                Username = Username,
                Nickname = Username,
                BannedUntil = BannedUntilUtc
            };
            Authentication = new AuthSessionManager(new StoredAccountRepository(StoredUser), null!, Flow.Time);
            SessionConnectionManager connections = new SessionConnectionManager(Flow.Registry, Flow.Lobbies);
            service = new AuthGrpcService(Authentication, connections, null!);
        }

        public ReportFlowBuilder Flow { get; }

        public AuthSessionManager Authentication { get; }

        public User StoredUser { get; }

        public DateTime BannedUntilUtc { get; }

        public User Authenticate()
        {
            return Authentication.AuthenticateUser(Username, Password);
        }

        public LoginResponse Login()
        {
            return LoginWithPassword(Password);
        }

        public LoginResponse LoginWithPassword(string password)
        {
            LoginRequest request = new LoginRequest { Username = Username, Password = password };
            return service.Login(request, null!).GetAwaiter().GetResult();
        }

        private sealed class StoredAccountRepository : AuthRepositoryService
        {
            private readonly User account;

            public StoredAccountRepository(User account)
            {
                this.account = account;
            }

            public override User AuthenticateUser(string username, string password)
            {
                if (username != account.Username || password != Password)
                {
                    throw new InvalidCredentialsException("Invalid test credentials.");
                }
                return account;
            }
        }
    }
}
