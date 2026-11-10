using RiskGame.Contracts.Friend;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class FriendSessionManager
    {
        private const int MaximumUsernameLength = 50;
        private const int MinimumRequestIdentifier = 1;
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

        public PendingFriendRequestList GetPendingRequests(FriendInboxRequest inboxRequest)
        {
            ValidateUser(inboxRequest.UserId, inboxRequest.SessionToken);
            return friendRepository.GetPendingRequests(inboxRequest.UserId);
        }

        public void RespondToRequest(FriendRequestReply requestReply)
        {
            ValidateUser(requestReply.UserId, requestReply.SessionToken);
            if (requestReply.RequestId < MinimumRequestIdentifier)
            {
                throw new FriendRequestRejectedException("A valid friendship request identifier is required.");
            }
            friendRepository.RespondToRequest(requestReply.UserId, requestReply.RequestId, requestReply.Accept);
        }

        private void ValidateUser(int userId, string sessionToken)
        {
            int authenticatedUserId = sessionRegistry.GetUserId(sessionToken);
            if (userId != authenticatedUserId)
            {
                throw new InvalidSessionException("The requested user does not match the active session.");
            }
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
