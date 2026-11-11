using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Invitation;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class InvitationGrpcService : Invitation.InvitationBase
    {
        private readonly InvitationSessionManager invitationSessionManager;

        public InvitationGrpcService(InvitationSessionManager invitationSessionManager)
        {
            this.invitationSessionManager = invitationSessionManager;
        }

        public override Task<PendingInvitationList> GetPendingInvitations(
            InvitationInboxRequest inboxRequest, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(invitationSessionManager.GetPendingInvitations(inboxRequest));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not read pending lobby invitations.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<LobbyDetails> AcceptInvitation(
            InvitationAction invitationAction, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(invitationSessionManager.AcceptInvitation(invitationAction));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not accept the lobby invitation.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<Empty> RejectInvitation(InvitationAction invitationAction, ServerCallContext callContext)
        {
            try
            {
                invitationSessionManager.RejectInvitation(invitationAction);
                return Task.FromResult(new Empty());
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not reject the lobby invitation.");
                throw CreateRpcException(dataOperationException);
            }
        }

        private RpcException CreateRpcException(DataOperationException dataOperationException)
        {
            StatusCode statusCode = StatusCode.Unavailable;
            if (dataOperationException is InvalidSessionException)
            {
                statusCode = StatusCode.Unauthenticated;
            }
            if (dataOperationException is InvitationUnavailableException)
            {
                statusCode = StatusCode.FailedPrecondition;
            }
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }
    }
}
