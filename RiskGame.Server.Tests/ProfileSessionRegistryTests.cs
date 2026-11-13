using RiskGame.Exceptions;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ProfileSessionRegistryTests
    {
        private const int SampleUserId = 42;
        private const string UnknownSessionToken = "nonexistent-session-token";

        [Fact]
        public void Register_ValidUser_ReturnsNonEmptyToken()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();

            string sessionToken = sessionRegistry.Register(SampleUserId);

            Assert.False(string.IsNullOrWhiteSpace(sessionToken));
        }

        [Fact]
        public void GetUserId_RegisteredToken_ReturnsExpectedUserId()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();
            string sessionToken = sessionRegistry.Register(SampleUserId);

            int resolvedUserId = sessionRegistry.GetUserId(sessionToken);

            Assert.Equal(SampleUserId, resolvedUserId);
        }

        [Fact]
        public void IsUserOnline_RegisteredUser_ReturnsTrue()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();
            sessionRegistry.Register(SampleUserId);

            bool isOnline = sessionRegistry.IsUserOnline(SampleUserId);

            Assert.True(isOnline);
        }

        [Fact]
        public void RemoveSession_ActiveToken_MarksUserOffline()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();
            string sessionToken = sessionRegistry.Register(SampleUserId);

            sessionRegistry.RemoveSession(sessionToken);

            Assert.False(sessionRegistry.IsUserOnline(SampleUserId));
        }

        [Fact]
        public void Register_SameUserAgain_InvalidatesPreviousToken()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();
            string firstSessionToken = sessionRegistry.Register(SampleUserId);
            sessionRegistry.Register(SampleUserId);

            Assert.Throws<InvalidSessionException>(() => sessionRegistry.GetUserId(firstSessionToken));
        }

        [Fact]
        public void GetUserId_UnknownToken_ThrowsInvalidSessionException()
        {
            ProfileSessionRegistry sessionRegistry = new ProfileSessionRegistry();

            Assert.Throws<InvalidSessionException>(() => sessionRegistry.GetUserId(UnknownSessionToken));
        }
    }
}
