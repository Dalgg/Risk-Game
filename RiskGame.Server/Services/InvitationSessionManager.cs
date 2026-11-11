using RiskGame.Contracts.Common;
using RiskGame.Contracts.Invitation;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class InvitationSessionManager
    {
        private const int MinimumInvitationIdentifier = 1;
        private readonly object invitationLock = new object();
        private readonly InvitationRepositoryService invitationRepository;
        private readonly LobbySessionManager lobbySessionManager;
        private readonly ProfileSessionRegistry sessionRegistry;

        public InvitationSessionManager(InvitationRepositoryService invitationRepository,
            LobbySessionManager lobbySessionManager, ProfileSessionRegistry sessionRegistry)
        {
            this.invitationRepository = invitationRepository;
            this.lobbySessionManager = lobbySessionManager;
            this.sessionRegistry = sessionRegistry;
        }

        public PendingInvitationList GetPendingInvitations(InvitationInboxRequest inboxRequest)
        {
            ValidateUser(inboxRequest.UserId, inboxRequest.SessionToken);
            return invitationRepository.GetPendingInvitations(inboxRequest.UserId);
        }

        public LobbyDetails AcceptInvitation(InvitationAction invitationAction)
        {
            ValidateAction(invitationAction);
            lock (invitationLock)
            {
                LobbyInvitation invitation = invitationRepository.GetPendingInvitation(
                    invitationAction.UserId, invitationAction.InvitationId);
                PlayerSummary joiningPlayer = BuildPlayer(invitation.IdRecipientNavigation);
                LobbyDetails lobbyDetails = lobbySessionManager.JoinInvitedLobby(invitation.IdLobby, joiningPlayer);
                try
                {
                    invitationRepository.CompleteInvitation(invitationAction.UserId,
                        invitationAction.InvitationId, true);
                }
                catch (DataOperationException dataOperationException)
                {
                    JsonLogger.Error(dataOperationException, "Could not finalize the accepted lobby invitation.");
                    lobbySessionManager.LeaveLobby(invitationAction.UserId);
                    throw;
                }
                return lobbyDetails;
            }
        }

        public void RejectInvitation(InvitationAction invitationAction)
        {
            ValidateAction(invitationAction);
            lock (invitationLock)
            {
                invitationRepository.CompleteInvitation(invitationAction.UserId,
                    invitationAction.InvitationId, false);
            }
        }

        private void ValidateUser(int userId, string sessionToken)
        {
            if (sessionRegistry.GetUserId(sessionToken) != userId)
            {
                throw new InvalidSessionException("The user does not match the active session.");
            }
        }

        private void ValidateAction(InvitationAction invitationAction)
        {
            ValidateUser(invitationAction.UserId, invitationAction.SessionToken);
            if (invitationAction.InvitationId < MinimumInvitationIdentifier)
            {
                throw new InvitationUnavailableException("A valid invitation identifier is required.");
            }
        }

        private PlayerSummary BuildPlayer(User? registeredUser)
        {
            if (registeredUser == null)
            {
                throw new InvitationUnavailableException("The invitation recipient no longer exists.");
            }
            string avatarReference = string.Empty;
            if (registeredUser.AvatarReference != null)
            {
                avatarReference = registeredUser.AvatarReference;
            }
            return new PlayerSummary
            {
                UserId = registeredUser.IdUser,
                Nickname = registeredUser.Nickname,
                AvatarReference = avatarReference,
                IsGuest = false
            };
        }
    }
}
