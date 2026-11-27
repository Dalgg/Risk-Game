using System;
using RiskGame.Exceptions;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class SuspendedSessionTests
    {
        private const int SuspensionHours = 24;
        private const int InitialAndTerminalMessages = 2;

        [Fact]
        public void Subscribe_SuspendedAccount_SendsExplicitSuspensionState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            connection.ReadInitialState();

            Assert.True(connection.InitialState.IsSuspended);
        }

        [Fact]
        public void Subscribe_SuspendedAccount_TerminalStateIsUnauthenticated()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            connection.ReadInitialState();

            Assert.False(connection.InitialState.IsAuthenticated);
        }

        [Fact]
        public void Subscribe_SuspendedAccount_SendsUtcExpiryAsUnixMilliseconds()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            connection.ReadInitialState();

            Assert.Equal(new DateTimeOffset(untilUtc).ToUnixTimeMilliseconds(),
                connection.InitialState.BannedUntilUnixMilliseconds);
        }

        [Fact]
        public void Subscribe_SuspendedAccount_EndsNormallyAfterTerminalState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            connection.ReadInitialState();

            Assert.Null(connection.WaitForCompletion());
        }

        [Fact]
        public void Subscribe_SuspensionDuringExpiryCleanup_PreservesFinalNotification()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            setup.Session.Registry.ExpireSessions();
            connection.ReadInitialState();

            Assert.True(connection.InitialState.IsSuspended);
        }

        [Fact]
        public void Subscribe_Suspension_EmitsOnlyInitialAndTerminalState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            connection.WaitForCompletion();

            Assert.Equal(InitialAndTerminalMessages, connection.MessageCount);
        }

        [Fact]
        public void ManualTimeProvider_AdvanceTwentyFourHours_AdvancesUtcClock()
        {
            ManualTimeProvider time = new ManualTimeProvider();
            DateTimeOffset initialUtc = time.GetUtcNow();

            time.Advance(TimeSpan.FromHours(SuspensionHours));

            Assert.Equal(initialUtc.AddHours(SuspensionHours), time.GetUtcNow());
        }

        [Fact]
        public void Subscribe_DisconnectedBeforeSuspension_ReplaysNoticeWithinGrace()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe previous = setup.Subscribe();
            previous.CancelAndWait();
            DateTime untilUtc = setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;
            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            setup.Session.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeforeGraceSeconds));
            setup.Session.Registry.ExpireSessions();

            SessionConnectionBuilder.ConnectionProbe replacement = setup.Subscribe();

            Assert.True(replacement.InitialState.IsSuspended);
        }

        [Fact]
        public void ExpireSessions_SuspendedTokenAfterDisconnectedGrace_RemovesNoticeMetadata()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.AttachFirstConnection();
            DateTime untilUtc = setup.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;
            setup.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);
            setup.Registry.DetachSession(setup.Token, SingleSessionBuilder.FirstConnectionId);
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.GraceSeconds));

            setup.Registry.ExpireSessions();

            Assert.Throws<InvalidSessionException>(() => setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void ValidateUser_SuspendedTokenOwnedByCaller_ReturnsSuspension()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            DateTime untilUtc = setup.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);

            Assert.Throws<AccountSuspendedException>(() =>
                setup.Registry.ValidateUser(SingleSessionBuilder.UserId, setup.Token));
        }

        [Fact]
        public void ValidateUser_SuspendedTokenOwnedByAnotherAccount_DoesNotRevealSuspension()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            DateTime untilUtc = setup.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;

            setup.Registry.SuspendUser(SingleSessionBuilder.UserId, untilUtc);

            Assert.Throws<InvalidSessionException>(() =>
                setup.Registry.ValidateUser(SingleSessionBuilder.OtherUserId, setup.Token));
        }
    }
}
