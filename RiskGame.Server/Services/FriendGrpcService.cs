using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Friend;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class FriendGrpcService : Friend.FriendBase
    {
        private readonly FriendSessionManager friendSessionManager;

        public FriendGrpcService(FriendSessionManager friendSessionManager)
        {
            this.friendSessionManager = friendSessionManager;
        }

        public override Task<Empty> SendFriendRequest(FriendRequest friendRequest, ServerCallContext callContext)
        {
            try
            {
                friendSessionManager.SendFriendRequest(friendRequest);
                return Task.FromResult(new Empty());
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not send the requested friendship invitation.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<PendingFriendRequestList> GetPendingRequests(
            FriendInboxRequest inboxRequest, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(friendSessionManager.GetPendingRequests(inboxRequest));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not read the friendship inbox.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<Empty> RespondToRequest(FriendRequestReply requestReply, ServerCallContext callContext)
        {
            try
            {
                friendSessionManager.RespondToRequest(requestReply);
                return Task.FromResult(new Empty());
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not respond to a pending friendship request.");
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
            if (dataOperationException is FriendRequestRejectedException)
            {
                statusCode = StatusCode.FailedPrecondition;
            }
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }

    }
}
