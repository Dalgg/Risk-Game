using System;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class SessionConnectionTests
    {
        private const int InitialMessageCount = 1;

        [Fact]
        public void Subscribe_ValidIdentity_ReturnsAuthenticatedInitialState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();

            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            Assert.True(connection.InitialState.IsAuthenticated);
        }

        [Fact]
        public void Subscribe_WaitsWithoutBusinessHeartbeat_SendsOnlyInitialState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            SessionConnectionBuilder.WaitThroughConnectionCheck();

            Assert.Equal(InitialMessageCount, connection.MessageCount);
        }

        [Fact]
        public void Subscribe_ConnectedBeyondReservationGrace_RemainsOpen()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            setup.Session.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeyondGraceSeconds));

            SessionConnectionBuilder.WaitThroughConnectionCheck();

            Assert.False(connection.IsCompleted);
        }

        [Fact]
        public void Subscribe_CancelledThenReconnectedDuringGrace_ReusesSameToken()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe previous = setup.Subscribe();
            previous.CancelAndWait();
            setup.Session.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeforeGraceSeconds));

            SessionConnectionBuilder.ConnectionProbe replacement = setup.Subscribe();

            Assert.True(replacement.InitialState.IsAuthenticated);
        }

        [Fact]
        public void Subscribe_SessionRevoked_EndsWithInvalidSession()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            setup.Session.Registry.RemoveSession(setup.Session.Token);

            Assert.IsType<InvalidSessionException>(connection.WaitForCompletion());
        }

        [Fact]
        public void Subscribe_ReplacedStream_OldCleanupPreservesNewConnection()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe previous = setup.Subscribe();
            setup.Subscribe();
            previous.WaitForCompletion();

            setup.Session.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeyondGraceSeconds));

            Assert.Equal(SingleSessionBuilder.UserId, setup.Session.Registry.GetUserId(setup.Session.Token));
        }

        [Fact]
        public void Subscribe_CrossedUserIdentifier_Rejects()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();

            SessionConnectionBuilder.ConnectionProbe connection = setup.StartForUser(SingleSessionBuilder.OtherUserId);

            Assert.IsType<InvalidSessionException>(connection.WaitForCompletion());
        }

        [Fact]
        public void Subscribe_CrossedIdentity_DoesNotDetachAuthenticatedConnection()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            setup.Subscribe();
            SessionConnectionBuilder.ConnectionProbe unauthorized =
                setup.StartForUser(SingleSessionBuilder.OtherUserId);
            unauthorized.WaitForCompletion();

            setup.Session.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeyondGraceSeconds));

            Assert.Equal(SingleSessionBuilder.UserId, setup.Session.Registry.GetUserId(setup.Session.Token));
        }

        [Fact]
        public void ScoreboardIdentity_TokenBelongsToAnotherAccount_Rejects()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            GrpcSessionAuthenticator authenticator = new GrpcSessionAuthenticator(setup.Registry);

            Assert.Throws<InvalidSessionException>(() =>
                authenticator.GetRegisteredUserId(SingleSessionBuilder.OtherUserId, setup.Token));
        }

        [Fact]
        public void ScoreboardIdentity_GuestIdentifier_Rejects()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            GrpcSessionAuthenticator authenticator = new GrpcSessionAuthenticator(setup.Registry);

            Assert.Throws<GuestNotAllowedException>(() =>
                authenticator.GetRegisteredUserId(SingleSessionBuilder.GuestUserId, setup.Token));
        }
    }
}
