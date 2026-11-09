using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Common;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class ChatGrpcService : Chat.ChatBase
    {
        private const string DefaultGuestNickname = "Guest";
        private readonly LobbySessionManager sessionManager;

        public ChatGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<Empty> SendMessage(ChatMessage incomingMessage, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary senderPlayer = EnsureValidSender(incomingMessage.Sender);
                sessionManager.SendLobbyChatMessage(senderPlayer, incomingMessage.Text);
                return Task.FromResult(new Empty());
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, lobbyNotFoundException.Message));
            }
        }

        public override Task SubscribeChat(
            ChatChannel channelRequest,
            IServerStreamWriter<ChatMessage> responseStream,
            ServerCallContext callContext)
        {
            try
            {
                sessionManager.RegisterChatSubscriber(channelRequest.UserId, responseStream);

                callContext.CancellationToken.WaitHandle.WaitOne();
                sessionManager.UnregisterChatSubscriber(channelRequest.UserId);
                return Task.CompletedTask;
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, lobbyNotFoundException.Message));
            }
        }

        private static PlayerSummary EnsureValidSender(PlayerSummary? requestedSender)
        {
            if (requestedSender != null && !string.IsNullOrWhiteSpace(requestedSender.Nickname))
            {
                return requestedSender;
            }

            return new PlayerSummary
            {
                UserId = 0,
                Nickname = DefaultGuestNickname,
                AvatarReference = string.Empty,
                IsGuest = true
            };
        }
    }
}
