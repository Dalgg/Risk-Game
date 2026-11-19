using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Callbacks;
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class LobbyViewModel : ViewModelBase
    {
        private const int DefaultMaxPlayers = 4;
        private const int MinimumValidMatchId = 1;
        private CancellationTokenSource? streamCancellation;
        private CancellationTokenSource? chatStreamCancellation;

        public LobbyDetails CreateLobby(RulesVariant rulesVariant, int turnDurationSeconds)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                CreateLobbyRequest createRequest = new CreateLobbyRequest
                {
                    Player = BuildCurrentPlayerSummary(),
                    Rules = new LobbyRules
                    {
                        RulesVariant = rulesVariant,
                        TurnDurationSeconds = turnDurationSeconds,
                        MaxPlayers = DefaultMaxPlayers,
                        IsPrivate = false
                    }
                };

                return lobbyClient.CreateLobby(createRequest);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public LobbyDetails JoinLobby(string roomCode)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                JoinLobbyRequest joinRequest = new JoinLobbyRequest
                {
                    LobbyCode = roomCode,
                    Player = BuildCurrentPlayerSummary()
                };

                return lobbyClient.JoinLobby(joinRequest);
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.NotFound)
            {
                throw new LobbyNotFoundException(Strings.LobbyErrorNotFound, rpcException);
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.ResourceExhausted)
            {
                throw new LobbyFullException(Strings.LobbyErrorFull, rpcException);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public void LeaveLobby()
        {
            StopSubscription();
            StopChatSubscription();
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                UserLobbyRequest leaveRequest = BuildUserLobbyRequest();
                lobbyClient.LeaveLobby(leaveRequest);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while leaving lobby.");
            }
        }

        public int StartMatch()
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                UserLobbyRequest startRequest = BuildUserLobbyRequest();
                StartMatchResponse startResponse = lobbyClient.StartMatch(startRequest);
                return startResponse.MatchId;
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                throw new InvalidOperationException(rpcException.Status.Detail, rpcException);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public List<FriendSummary> GetFriends()
        {
            return FriendsViewModel.GetFriends();
        }

        public void InviteFriend(int friendUserId)
        {
            try
            {
                SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                InviteFriendRequest inviteRequest = new InviteFriendRequest
                {
                    SenderUserId = currentSessionUser.IdUser,
                    RecipientUserId = friendUserId
                };

                lobbyClient.InviteFriend(inviteRequest);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public void SendMessage(string messageText)
        {
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                try
                {
                    GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                    Chat.ChatClient chatClient = new Chat.ChatClient(grpcChannel);
                    ChatMessage outgoingMessage = new ChatMessage
                    {
                        Text = messageText.Trim(),
                        Sender = BuildCurrentPlayerSummary()
                    };

                    chatClient.SendMessage(outgoingMessage);
                }
                catch (RpcException rpcException)
                {
                    throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
                }
            }
        }

        public void SubscribeToEvents(ILobbyCallback lobbyCallback)
        {
            StopSubscription();
            streamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = streamCancellation.Token;
            Task.Run(() => ReadLobbyEventStream(lobbyCallback, cancellationToken));
        }

        private void ReadLobbyEventStream(ILobbyCallback lobbyCallback, CancellationToken cancellationToken)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                UserLobbyRequest subscribeRequest = BuildUserLobbyRequest();

                using AsyncServerStreamingCall<LobbyEvent> streamingCall = lobbyClient.SubscribeLobbyEvents(
                    subscribeRequest,
                    cancellationToken: cancellationToken);

                while (streamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                {
                    LobbyEvent currentLobbyEvent = streamingCall.ResponseStream.Current;
                    DispatchLobbyEvent(currentLobbyEvent, lobbyCallback);
                }
            }
            catch (RpcException rpcException)
            {
                if (!cancellationToken.IsCancellationRequested && rpcException.StatusCode != StatusCode.Cancelled)
                {
                    JsonLogger.Error(rpcException, "Lobby event stream disconnected with RpcException.");
                }
            }
            catch (OperationCanceledException canceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    JsonLogger.Error(canceledException, "Lobby event stream operation was cancelled unexpectedly.");
                }
            }
        }

        public void SubscribeChat(IChatCallback chatCallback)
        {
            StopChatSubscription();
            chatStreamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = chatStreamCancellation.Token;
            Task.Run(() => ReadChatStream(chatCallback, cancellationToken));
        }

        private void ReadChatStream(IChatCallback chatCallback, CancellationToken cancellationToken)
        {
            try
            {
                SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Chat.ChatClient chatClient = new Chat.ChatClient(grpcChannel);
                ChatChannel channelRequest = new ChatChannel
                {
                    UserId = currentSessionUser.IdUser
                };

                using AsyncServerStreamingCall<ChatMessage> chatStreamingCall = chatClient.SubscribeChat(
                    channelRequest,
                    cancellationToken: cancellationToken);

                while (chatStreamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                {
                    ChatMessage incomingMessage = chatStreamingCall.ResponseStream.Current;
                    chatCallback.MessageReceived(incomingMessage);
                }
            }
            catch (RpcException rpcException)
            {
                if (!cancellationToken.IsCancellationRequested && rpcException.StatusCode != StatusCode.Cancelled)
                {
                    JsonLogger.Error(rpcException, "Chat stream disconnected with RpcException.");
                }
            }
            catch (OperationCanceledException canceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    JsonLogger.Error(canceledException, "Chat stream operation was cancelled unexpectedly.");
                }
            }
        }

        public void StopSubscription()
        {
            if (streamCancellation != null)
            {
                streamCancellation.Cancel();
                streamCancellation.Dispose();
                streamCancellation = null;
            }
        }

        public void StopChatSubscription()
        {
            if (chatStreamCancellation != null)
            {
                chatStreamCancellation.Cancel();
                chatStreamCancellation.Dispose();
                chatStreamCancellation = null;
            }
        }

        private static void DispatchLobbyEvent(LobbyEvent currentLobbyEvent, ILobbyCallback lobbyCallback)
        {
            if (currentLobbyEvent.StartedMatchId >= MinimumValidMatchId)
            {
                lobbyCallback.MatchStarting(currentLobbyEvent.StartedMatchId);
            }
            else if (currentLobbyEvent.Lobby != null)
            {
                lobbyCallback.LobbyUpdated(currentLobbyEvent.Lobby);
            }
        }

        private static UserLobbyRequest BuildUserLobbyRequest()
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            return new UserLobbyRequest
            {
                UserId = currentSessionUser.IdUser
            };
        }

        private static PlayerSummary BuildCurrentPlayerSummary()
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            string avatarReference = string.Empty;
            if (currentSessionUser.AvatarReference != null)
            {
                avatarReference = currentSessionUser.AvatarReference;
            }

            return new PlayerSummary
            {
                UserId = currentSessionUser.IdUser,
                Nickname = currentSessionUser.Nickname,
                AvatarReference = avatarReference,
                IsGuest = currentSessionUser.IsGuest
            };
        }
    }
}
