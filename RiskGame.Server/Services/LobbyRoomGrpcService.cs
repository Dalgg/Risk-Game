using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class LobbyRoomGrpcService : LobbyRoom.LobbyRoomBase
    {
        private const string FaultCodeTrailerKey = "fault-code";
        private const string UserIdHeaderKey = "user-id";
        private const int BottomUserIdRange = 100000;
        private const int TopUserIdRange = 999999;
        private readonly LobbySessionManager sessionManager;

        public LobbyRoomGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<Int32Value> StartMatch(Empty startRequest, ServerCallContext callContext)
        {
            try
            {
                int requestingUserId = ExtractUserIdFromHeaders(callContext.RequestHeaders);
                int createdMatchId = sessionManager.StartMatch(requestingUserId);
                return Task.FromResult(new Int32Value { Value = createdMatchId });
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw CreateFaultException(StatusCode.NotFound, FaultCode.LobbyNotFound, lobbyNotFoundException.Message);
            }
            catch (InvalidOperationException invalidOperationException)
            {
                throw CreateFaultException(StatusCode.FailedPrecondition, FaultCode.NotEnoughPlayers, invalidOperationException.Message);
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, dataOperationException.Message);
            }
        }

        public override Task<FriendSummaryList> GetFriends(Empty request, ServerCallContext callContext)
        {
            try
            {
                int requestingUserId = ExtractUserIdFromHeaders(callContext.RequestHeaders);
                FriendSummaryList friends = sessionManager.GetFriends(requestingUserId);
                return Task.FromResult(friends);
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, dataOperationException.Message);
            }
        }

        public override Task<Empty> InviteFriend(Int32Value recipientUserRequest, ServerCallContext callContext)
        {
            try
            {
                int senderUserId = ExtractUserIdFromHeaders(callContext.RequestHeaders);
                int recipientUserId = recipientUserRequest.Value;
                sessionManager.InviteFriend(senderUserId, recipientUserId);
                return Task.FromResult(new Empty());
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw CreateFaultException(StatusCode.NotFound, FaultCode.LobbyNotFound, lobbyNotFoundException.Message);
            }
            catch (ArgumentException argumentException)
            {
                throw CreateFaultException(StatusCode.InvalidArgument, FaultCode.InvalidInvitation, argumentException.Message);
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, dataOperationException.Message);
            }
        }

        private static int ExtractUserIdFromHeaders(Metadata requestHeaders)
        {
            foreach (Metadata.Entry headerEntry in requestHeaders)
            {
                if (string.Equals(headerEntry.Key, UserIdHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseUserIdOrDefault(headerEntry.Value);
                }
            }

            return RandomNumberGenerator.GetInt32(BottomUserIdRange, TopUserIdRange);
        }

        private static int ParseUserIdOrDefault(string headerValue)
        {
            try
            {
                return int.Parse(headerValue);
            }
            catch (FormatException formatException)
            {
                JsonLogger.Error(formatException, "Invalid user-id header format in LobbyRoom request.");
                return 0;
            }
            catch (OverflowException overflowException)
            {
                JsonLogger.Error(overflowException, "Overflow in user-id header value in LobbyRoom request.");
                return 0;
            }
        }

        private static RpcException CreateFaultException(StatusCode statusCode, FaultCode faultCode, string errorMessage)
        {
            Metadata faultTrailers = new Metadata { { FaultCodeTrailerKey, faultCode.ToString() } };
            return new RpcException(new Status(statusCode, errorMessage), faultTrailers);
        }
    }
}
