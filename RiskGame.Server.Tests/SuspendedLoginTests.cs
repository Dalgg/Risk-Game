using System;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class SuspendedLoginTests
    {
        private const int OneTick = 1;
        private const int HoursAfterExpiry = 1;

        [Fact]
        public void AuthenticateUser_PersistedSuspension_RejectsValidCredentials()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            Assert.Throws<AccountSuspendedException>(() => setup.Authenticate());
        }

        [Fact]
        public void AuthenticateUser_OneTickBeforeExpiry_StillRejects()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.Flow.Time.Advance(TimeSpan.FromHours(SuspendedLoginBuilder.SuspensionHours)
                - TimeSpan.FromTicks(OneTick));

            Assert.Throws<AccountSuspendedException>(() => setup.Authenticate());
        }

        [Fact]
        public void AuthenticateUser_ExactlyAtExpiry_AllowsAccess()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.Flow.Time.Advance(TimeSpan.FromHours(SuspendedLoginBuilder.SuspensionHours));

            User account = setup.Authenticate();

            Assert.Equal(setup.StoredUser.IdUser, account.IdUser);
        }

        [Fact]
        public void AuthenticateUser_AfterExpiry_AllowsAccess()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.Flow.Time.Advance(TimeSpan.FromHours(SuspendedLoginBuilder.SuspensionHours + HoursAfterExpiry));

            User account = setup.Authenticate();

            Assert.Equal(setup.StoredUser.IdUser, account.IdUser);
        }

        [Fact]
        public void AuthenticateUser_NoSuspension_AllowsAccess()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.StoredUser.BannedUntil = null;

            User account = setup.Authenticate();

            Assert.Equal(setup.StoredUser.IdUser, account.IdUser);
        }

        [Fact]
        public void AuthenticateUser_WrongPassword_PreservesInvalidCredentialsError()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            Assert.Throws<InvalidCredentialsException>(() => setup.Authentication.AuthenticateUser(
                SuspendedLoginBuilder.Username, SuspendedLoginBuilder.WrongPassword));
        }

        [Fact]
        public void Login_SuspendedAccount_ReportsExplicitSuspension()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            LoginResponse response = setup.Login();

            Assert.True(response.IsSuspended);
        }

        [Fact]
        public void Login_SuspendedAccount_DoesNotAuthenticate()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            LoginResponse response = setup.Login();

            Assert.False(response.IsAuthenticated);
        }

        [Fact]
        public void Login_SuspendedAccount_DoesNotIssueToken()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            LoginResponse response = setup.Login();

            Assert.Empty(response.SessionToken);
        }

        [Fact]
        public void Login_SuspendedAccount_ReturnsUtcExpiry()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            LoginResponse response = setup.Login();

            Assert.Equal(new DateTimeOffset(setup.BannedUntilUtc).ToUnixTimeMilliseconds(),
                response.BannedUntilUnixMilliseconds);
        }

        [Fact]
        public void Login_DatabaseDateWithoutKind_InterpretsExpiryAsUtc()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.StoredUser.BannedUntil = DateTime.SpecifyKind(setup.BannedUntilUtc, DateTimeKind.Unspecified);

            LoginResponse response = setup.Login();

            Assert.Equal(new DateTimeOffset(setup.BannedUntilUtc).ToUnixTimeMilliseconds(),
                response.BannedUntilUnixMilliseconds);
        }

        [Fact]
        public void Login_WrongPassword_DoesNotRevealSuspension()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();

            LoginResponse response = setup.LoginWithPassword(SuspendedLoginBuilder.WrongPassword);

            Assert.False(response.IsSuspended);
        }

        [Fact]
        public void Login_ExactlyAtExpiry_Authenticates()
        {
            SuspendedLoginBuilder setup = new SuspendedLoginBuilder();
            setup.Flow.Time.Advance(TimeSpan.FromHours(SuspendedLoginBuilder.SuspensionHours));

            LoginResponse response = setup.Login();

            Assert.True(response.IsAuthenticated);
        }
    }
}
