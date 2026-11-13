using System;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Invitation;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class InvitationRepositoryService
    {
        private const string PendingStatus = "pending";
        private const string AcceptedStatus = "accepted";
        private const int NoUpdatedInvitations = 0;

        public PendingInvitationList GetPendingInvitations(int userId)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                DateTime currentServerDateTime = DateTime.UtcNow;
                PendingInvitationList invitations = new PendingInvitationList();
                invitations.Invitations.AddRange(databaseContext.LobbyInvitations.AsNoTracking()
                    .Where(invitation => invitation.IdRecipient == userId && invitation.Status == PendingStatus
                        && invitation.ExpirationDate > currentServerDateTime)
                    .Where(invitation => invitation.IdLobbyNavigation.ClosureDate == null)
                    .OrderBy(invitation => invitation.SentDate).ThenBy(invitation => invitation.IdInvitation)
                    .Select(invitation => new PendingInvitation
                    {
                        InvitationId = invitation.IdInvitation,
                        SenderUsername = invitation.IdSenderNavigation.Username,
                        LobbyCode = invitation.IdLobbyNavigation.LobbyCode
                    }).ToList());
                return invitations;
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not load lobby invitations for user {0}.", userId);
                throw new DataOperationException("Could not load lobby invitations.", databaseException);
            }
        }

        public LobbyInvitation GetPendingInvitation(int userId, int invitationId)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                LobbyInvitation? invitation = databaseContext.LobbyInvitations.AsNoTracking()
                    .Include(invitationRecord => invitationRecord.IdRecipientNavigation)
                    .FirstOrDefault(invitationRecord => invitationRecord.IdInvitation == invitationId
                        && invitationRecord.IdRecipient == userId && invitationRecord.Status == PendingStatus);
                if (invitation == null || invitation.ExpirationDate <= DateTime.UtcNow)
                {
                    throw new InvitationUnavailableException("The pending invitation is unavailable or expired.");
                }
                return invitation;
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not retrieve invitation {0}.", invitationId);
                throw new DataOperationException("Could not retrieve the invitation.", databaseException);
            }
        }

        public void CompleteInvitation(int userId, int invitationId, bool accepted)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                IQueryable<LobbyInvitation> matchingInvitations = databaseContext.LobbyInvitations
                    .Where(invitation => invitation.IdRecipient == userId
                        && invitation.IdInvitation == invitationId && invitation.Status == PendingStatus);
                int updatedInvitations;
                if (accepted)
                {
                    DateTime currentServerDateTime = DateTime.UtcNow;
                    updatedInvitations = matchingInvitations
                        .Where(invitation => invitation.ExpirationDate > currentServerDateTime)
                        .ExecuteUpdate(properties => properties.SetProperty(invitation => invitation.Status,
                            AcceptedStatus));
                }
                else
                {
                    updatedInvitations = matchingInvitations.ExecuteDelete();
                }
                if (updatedInvitations == NoUpdatedInvitations)
                {
                    throw new InvitationUnavailableException("The invitation is no longer pending.");
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not resolve invitation {0}.", invitationId);
                throw new DataOperationException("Could not resolve the invitation.", databaseException);
            }
        }
    }
}
