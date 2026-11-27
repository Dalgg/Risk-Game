using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class GrpcSessionAuthenticator
    {
        private const int MinimumRegisteredIdentifier = 1;
        private readonly ProfileSessionRegistry sessionRegistry;

        public GrpcSessionAuthenticator(ProfileSessionRegistry sessionRegistry)
        {
            this.sessionRegistry = sessionRegistry;
        }

        public int GetRegisteredUserId(int userId, string sessionToken)
        {
            if (userId < MinimumRegisteredIdentifier)
            {
                throw new GuestNotAllowedException("A registered account is required to access the scoreboard.");
            }
            sessionRegistry.ValidateUser(userId, sessionToken);
            return userId;
        }
    }
}
