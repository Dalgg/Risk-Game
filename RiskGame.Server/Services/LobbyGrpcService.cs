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
    public sealed class LobbyGrpcService : Lobby.LobbyBase
    {
        private const string FaultCodeTrailerKey = "fault-code";
        private const string UserIdHeaderKey = "user-id";
        private const string NicknameHeaderKey = "nickname";
        private const string AvatarHeaderKey = "avatar-reference";
        private const string IsGuestHeaderKey = "is-guest";
        private const string DefaultGuestNickname = "Guest";
        private const int BottomUserIdRange = 100000;
        private const int TopUserIdRange = 999999;
        private readonly LobbySessionManager sessionManager;

        public LobbyGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<LobbyDetails> CreateLobby(LobbyRules rulesRequest, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary hostPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
                LobbyDetails createdLobbyDetails = sessionManager.CreateLobby(rulesRequest, hostPlayer);
                return Task.FromResult(createdLobbyDetails);
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, dataOperationException.Message);
            }
        }

        public override Task<LobbyDetails> JoinLobby(StringValue joinRequest, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary joiningPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
                LobbyDetails lobbyDetails = sessionManager.JoinLobby(joinRequest.Value, joiningPlayer);
                return Task.FromResult(lobbyDetails);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw CreateFaultException(StatusCode.NotFound, FaultCode.LobbyNotFound, lobbyNotFoundException.Message);
            }
            catch (LobbyFullException lobbyFullException)
            {
                throw CreateFaultException(StatusCode.ResourceExhausted, FaultCode.LobbyFull, lobbyFullException.Message);
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, dataOperationException.Message);
            }
        }

        public override Task<Empty> LeaveLobby(Empty leaveRequest, ServerCallContext callContext)
        {
            PlayerSummary leavingPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
            sessionManager.LeaveLobby(leavingPlayer.UserId);
            return Task.FromResult(new Empty());
        }

        public override Task SubscribeLobbyEvents(
            Empty subscribeRequest,
            IServerStreamWriter<LobbyEvent> responseStream,
            ServerCallContext callContext)
        {
            try
            {
                PlayerSummary subscriberPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
                LobbyDetails initialLobbySnapshot = sessionManager.RegisterSubscriber(subscriberPlayer.UserId, responseStream);

                LobbyEvent snapshotEvent = new LobbyEvent
                {
                    LobbyChanged = new LobbyChanged
                    {
                        Change = LobbyChangeType.Snapshot,
                        Lobby = initialLobbySnapshot,
                        AffectedUserId = subscriberPlayer.UserId
                    }
                };

                responseStream.WriteAsync(snapshotEvent).GetAwaiter().GetResult();

                callContext.CancellationToken.WaitHandle.WaitOne();
                sessionManager.UnregisterSubscriber(subscriberPlayer.UserId);
                return Task.CompletedTask;
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw CreateFaultException(StatusCode.NotFound, FaultCode.LobbyNotFound, lobbyNotFoundException.Message);
            }
        }

        private static PlayerSummary ExtractPlayerFromHeaders(Metadata requestHeaders)
        {
            int parsedUserId = 0;
            string playerNickname = DefaultGuestNickname;
            string avatarReference = string.Empty;
            bool isGuestPlayer = true;

            foreach (Metadata.Entry headerEntry in requestHeaders)
            {
                if (string.Equals(headerEntry.Key, UserIdHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    parsedUserId = ParseUserIdOrDefault(headerEntry.Value);
                }
                else if (string.Equals(headerEntry.Key, NicknameHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    playerNickname = headerEntry.Value;
                }
                else if (string.Equals(headerEntry.Key, AvatarHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    avatarReference = headerEntry.Value;
                }
                else if (string.Equals(headerEntry.Key, IsGuestHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    isGuestPlayer = ParseBooleanOrDefault(headerEntry.Value, true);
                }
            }

            if (parsedUserId == 0)
            {
                parsedUserId = RandomNumberGenerator.GetInt32(BottomUserIdRange, TopUserIdRange);
            }

            return new PlayerSummary
            {
                UserId = parsedUserId,
                Nickname = playerNickname,
                AvatarReference = avatarReference,
                IsGuest = isGuestPlayer
            };
        }

        private static int ParseUserIdOrDefault(string headerValue)
        {
            try
            {
                return int.Parse(headerValue);
            }
            catch (FormatException formatException)
            {
                JsonLogger.Error(formatException, "Invalid user-id header format.");
                return 0;
            }
            catch (OverflowException overflowException)
            {
                JsonLogger.Error(overflowException, "Overflow in user-id header value.");
                return 0;
            }
        }

        private static bool ParseBooleanOrDefault(string headerValue, bool defaultValue)
        {
            if (string.Equals(headerValue, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(headerValue, bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }

        private static RpcException CreateFaultException(StatusCode statusCode, FaultCode faultCode, string errorMessage)
        {
            Metadata faultTrailers = new Metadata { { FaultCodeTrailerKey, faultCode.ToString() } };
            return new RpcException(new Status(statusCode, errorMessage), faultTrailers);
        }
    }
}
