using System;
using RiskGame.Exceptions;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class SingleSessionTests
    {
        [Fact]
        public void Register_SecondLogin_PreservesOriginalToken()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            setup.AttemptSecondLogin();

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void Register_SimultaneousRequests_AcceptsOnlyOneLogin()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            int acceptedLogins = setup.RegisterConcurrently();

            Assert.Equal(SingleSessionBuilder.OneAcceptedLogin, acceptedLogins);
        }

        [Fact]
        public void Register_DifferentUser_AllowsIndependentSession()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            string otherToken = setup.Registry.Register(SingleSessionBuilder.OtherUserId);

            Assert.Equal(SingleSessionBuilder.OtherUserId, setup.Registry.GetUserId(otherToken));
        }

        [Fact]
        public void RemoveSession_ThenLogin_AllowsSameAccountAgain()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.Registry.RemoveSession(setup.Token);

            string replacementToken = setup.Registry.Register(SingleSessionBuilder.UserId);

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(replacementToken));
        }

        [Fact]
        public void Register_NoConnectionAttached_ExpiresReservationAfterGrace()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.GraceSeconds));

            string replacementToken = setup.Registry.Register(SingleSessionBuilder.UserId);

            Assert.NotEqual(setup.Token, replacementToken);
        }

        [Fact]
        public void AttachSession_ConnectedBeyondGrace_PreservesSession()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.AttachFirstConnection();

            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeyondGraceSeconds));

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void DetachSession_DuringGrace_RejectsSecondLogin()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.DisconnectFirstConnection();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeforeGraceSeconds));

            Assert.Throws<SessionAlreadyActiveException>(() => setup.Registry.Register(SingleSessionBuilder.UserId));
        }

        [Fact]
        public void DetachSession_GraceExpires_AllowsNewLogin()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.DisconnectFirstConnection();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.GraceSeconds));

            string replacementToken = setup.Registry.Register(SingleSessionBuilder.UserId);

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(replacementToken));
        }

        [Fact]
        public void AttachSession_ReconnectedDuringGrace_KeepsOriginalTokenValid()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            setup.ReconnectBeforeGraceExpires();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.BeyondGraceSeconds));

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void AttachSession_ReplacesConnection_OldCleanupDoesNotDetachReplacement()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.AttachFirstConnection();
            setup.Registry.AttachSession(SingleSessionBuilder.UserId, setup.Token,
                SingleSessionBuilder.ReplacementConnectionId);

            setup.Registry.DetachSession(setup.Token, SingleSessionBuilder.FirstConnectionId);

            Assert.True(setup.Registry.IsConnectionCurrent(setup.Token,
                SingleSessionBuilder.ReplacementConnectionId));
        }

        [Fact]
        public void RemoveSession_OldToken_DoesNotRemoveLaterSession()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.Registry.RemoveSession(setup.Token);
            string replacementToken = setup.Registry.Register(SingleSessionBuilder.UserId);

            setup.Registry.RemoveSession(setup.Token);

            Assert.Equal(SingleSessionBuilder.UserId, setup.Registry.GetUserId(replacementToken));
        }

        [Fact]
        public void DetachSession_OldToken_DoesNotChangeLaterConnection()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.Registry.RemoveSession(setup.Token);
            string replacementToken = setup.Registry.Register(SingleSessionBuilder.UserId);
            setup.Registry.AttachSession(SingleSessionBuilder.UserId, replacementToken,
                SingleSessionBuilder.ReplacementConnectionId);

            setup.Registry.DetachSession(setup.Token, SingleSessionBuilder.FirstConnectionId);

            Assert.True(setup.Registry.IsConnectionCurrent(replacementToken,
                SingleSessionBuilder.ReplacementConnectionId));
        }

        [Fact]
        public void ValidateUser_TokenBelongsToAnotherAccount_Rejects()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            Assert.Throws<InvalidSessionException>(() =>
                setup.Registry.ValidateUser(SingleSessionBuilder.OtherUserId, setup.Token));
        }

        [Fact]
        public void AttachSession_TokenBelongsToAnotherAccount_Rejects()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            Assert.Throws<InvalidSessionException>(() => setup.Registry.AttachSession(
                SingleSessionBuilder.OtherUserId, setup.Token, SingleSessionBuilder.FirstConnectionId));
        }

        [Fact]
        public void IsSessionValid_TokenBelongsToAnotherAccount_ReturnsFalse()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            bool valid = setup.Registry.IsSessionValid(SingleSessionBuilder.OtherUserId, setup.Token);

            Assert.False(valid);
        }

        [Fact]
        public void ExpireSessions_ExpiredGrace_RemovesOnlinePresence()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.DisconnectFirstConnection();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.GraceSeconds));

            setup.Registry.ExpireSessions();

            Assert.False(setup.Registry.IsUserOnline(SingleSessionBuilder.UserId));
        }

        [Fact]
        public void GetUserId_DisconnectedGraceExpires_RejectsOldToken()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.DisconnectFirstConnection();
            setup.Time.Advance(TimeSpan.FromSeconds(SingleSessionBuilder.GraceSeconds));

            Assert.Throws<InvalidSessionException>(() => setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void GetUserId_AbsoluteLifetimeExpires_RejectsConnectedToken()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            setup.AttachFirstConnection();
            setup.Time.Advance(TimeSpan.FromHours(SingleSessionBuilder.LifetimeHours));

            Assert.Throws<InvalidSessionException>(() => setup.Registry.GetUserId(setup.Token));
        }

        [Fact]
        public void Register_GuestIdentifier_RetainsExistingBehavior()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            string guestToken = setup.Registry.Register(SingleSessionBuilder.GuestUserId);

            Assert.Equal(SingleSessionBuilder.GuestUserId, setup.Registry.GetUserId(guestToken));
        }
    }
}
