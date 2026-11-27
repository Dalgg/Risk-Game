using System;
using System.Threading;
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
        private const int StreamSessionCheckSeconds = 1;
        private const int InvalidUserIdentifier = 0;
        private readonly LobbySessionManager sessionManager;

        public ChatGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<Empty> SendMessage(SendChatRequest request, ServerCallContext callContext)
        {
            try
            {
                ChatMessage incomingMessage = request.Message;
                if (incomingMessage == null)
                {
                    throw new ArgumentException("A chat message is required.");
                }
                PlayerSummary senderPlayer = EnsureValidSender(incomingMessage.Sender);
                ValidateParticipant(senderPlayer.UserId, request.SessionToken);
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
                return ReadChatSession(channelRequest, responseStream, callContext.CancellationToken);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, lobbyNotFoundException.Message));
            }
        }

        private static PlayerSummary EnsureValidSender(PlayerSummary? requestedSender)
        {
            if (requestedSender == null || requestedSender.UserId == InvalidUserIdentifier
                || string.IsNullOrWhiteSpace(requestedSender.Nickname))
            {
                throw new ArgumentException("A valid chat sender is required.");
            }
            PlayerSummary sender = requestedSender.Clone();
            sender.IsGuest = sender.UserId < InvalidUserIdentifier;
            return sender;
        }

        private void ValidateParticipant(int userId, string sessionToken)
        {
            try
            {
                sessionManager.ValidateParticipant(userId, sessionToken);
            }
            catch (InvalidSessionException invalidSessionException)
            {
                JsonLogger.Error(invalidSessionException, "The chat operation requires a valid player session.");
                throw new RpcException(new Status(StatusCode.Unauthenticated, invalidSessionException.Message));
            }
        }

        private Task ReadChatSession(ChatChannel request, IServerStreamWriter<ChatMessage> stream,
            CancellationToken cancellationToken)
        {
            ValidateParticipant(request.UserId, request.SessionToken);
            try
            {
                sessionManager.RegisterChatSubscriber(request.UserId, stream);
                WaitForChatSession(request, stream, cancellationToken);
                return Task.CompletedTask;
            }
            finally
            {
                sessionManager.UnregisterChatSubscriber(request.UserId, stream);
            }
        }

        private void WaitForChatSession(ChatChannel request, IServerStreamWriter<ChatMessage> stream,
            CancellationToken cancellationToken)
        {
            TimeSpan interval = TimeSpan.FromSeconds(StreamSessionCheckSeconds);
            bool current = true;
            while (current && !cancellationToken.WaitHandle.WaitOne(interval))
            {
                ValidateParticipant(request.UserId, request.SessionToken);
                current = sessionManager.IsChatSubscriberCurrent(request.UserId, stream);
            }
        }
    }
}
