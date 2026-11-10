using RiskGame.Contracts.Friend;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class FriendSessionManager
    {
        private const int MaximumUsernameLength = 50;
        private readonly FriendRepositoryService friendRepository;
        private readonly ProfileSessionRegistry sessionRegistry;

        public FriendSessionManager(FriendRepositoryService friendRepository, ProfileSessionRegistry sessionRegistry)
        {
            this.friendRepository = friendRepository;
            this.sessionRegistry = sessionRegistry;
        }

        public void SendFriendRequest(FriendRequest friendRequest)
        {
            int authenticatedUserId = sessionRegistry.GetUserId(friendRequest.SessionToken);
            if (authenticatedUserId != friendRequest.UserId)
            {
                throw new InvalidSessionException("The sender does not match the active session.");
            }

            string targetUsername = friendRequest.TargetUsername.Trim();
            ValidateUsername(targetUsername);
            friendRepository.SendFriendRequest(authenticatedUserId, targetUsername);
        }

        private void ValidateUsername(string targetUsername)
        {
            if (string.IsNullOrWhiteSpace(targetUsername) || targetUsername.Length > MaximumUsernameLength)
            {
                throw new FriendRequestRejectedException("A valid target username is required.");
            }
        }
    }
}
