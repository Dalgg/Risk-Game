using System;
using System.Threading;
using Grpc.Net.Client;
using RiskGame.Contracts.Friend;
using RiskGame.Contracts.Invitation;

namespace RiskGame.Services
{
    public class InboxNotificationClient
    {
        private const int EmptyInboxCount = 0;
        private const int RequestTimeoutSeconds = 10;

        public virtual bool HasPending(SessionUser session, CancellationToken cancellationToken)
        {
            PendingFriendRequestList requests = QueryRequests(session, cancellationToken);
            bool hasPending = requests.Requests.Count > EmptyInboxCount;
            if (!hasPending)
            {
                PendingInvitationList invitations = QueryInvitations(session, cancellationToken);
                hasPending = invitations.Invitations.Count > EmptyInboxCount;
            }
            return hasPending;
        }

        private static PendingFriendRequestList QueryRequests(SessionUser session, CancellationToken cancellationToken)
        {
            FriendInboxRequest request = new FriendInboxRequest
            {
                UserId = session.IdUser, SessionToken = session.SessionToken
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Friend.FriendClient client = new Friend.FriendClient(channel);
            return client.GetPendingRequests(request, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: cancellationToken);
        }

        private static PendingInvitationList QueryInvitations(SessionUser session, CancellationToken cancellationToken)
        {
            InvitationInboxRequest request = new InvitationInboxRequest
            {
                UserId = session.IdUser, SessionToken = session.SessionToken
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Invitation.InvitationClient client = new Invitation.InvitationClient(channel);
            return client.GetPendingInvitations(request, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: cancellationToken);
        }
    }
}
