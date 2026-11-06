using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Common;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class ChatGrpcService : Chat.ChatBase
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

        public ChatGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<Empty> SendMessage(ChatMessage incomingMessage, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary senderPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
                sessionManager.SendLobbyChatMessage(senderPlayer, incomingMessage.Text);
                return Task.FromResult(new Empty());
            }
            catch (ArgumentException argumentException)
            {
                throw CreateFaultException(StatusCode.InvalidArgument, FaultCode.MessageTooLong, argumentException.Message);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw CreateFaultException(StatusCode.FailedPrecondition, FaultCode.NotInLobby, lobbyNotFoundException.Message);
            }
        }

        public override Task SubscribeChat(
            ChatChannel channelRequest,
            IServerStreamWriter<ChatMessage> responseStream,
            ServerCallContext callContext)
        {
            PlayerSummary subscriberPlayer = ExtractPlayerFromHeaders(callContext.RequestHeaders);
            sessionManager.RegisterChatSubscriber(subscriberPlayer.UserId, responseStream);

            callContext.CancellationToken.WaitHandle.WaitOne();
            sessionManager.UnregisterChatSubscriber(subscriberPlayer.UserId);
            return Task.CompletedTask;
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
                JsonLogger.Error(formatException, "Invalid user-id header format in Chat request.");
                return 0;
            }
            catch (OverflowException overflowException)
            {
                JsonLogger.Error(overflowException, "Overflow in user-id header value in Chat request.");
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
