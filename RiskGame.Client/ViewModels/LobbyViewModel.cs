using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
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
        private const string UserIdHeaderKey = "user-id";
        private const string NicknameHeaderKey = "nickname";
        private const string AvatarHeaderKey = "avatar-reference";
        private const string IsGuestHeaderKey = "is-guest";
        private const int DefaultMaxPlayers = 4;
        private CancellationTokenSource? streamCancellation;
        private CancellationTokenSource? chatStreamCancellation;

        public LobbyDetails CreateLobby(RulesVariant rulesVariant, int turnDurationSeconds)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                LobbyRules lobbyRules = new LobbyRules
                {
                    RulesVariant = rulesVariant,
                    TurnDurationSeconds = turnDurationSeconds,
                    MaxPlayers = DefaultMaxPlayers,
                    IsPrivate = false
                };

                return lobbyClient.CreateLobby(lobbyRules, requestHeaders);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while creating lobby.");
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public LobbyDetails JoinLobby(string roomCode)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                return lobbyClient.JoinLobby(new StringValue { Value = roomCode }, requestHeaders);
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
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while joining lobby {0}.", roomCode);
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
                Metadata requestHeaders = BuildPlayerHeaders();
                lobbyClient.LeaveLobby(new Empty(), requestHeaders);
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
                LobbyRoom.LobbyRoomClient lobbyRoomClient = new LobbyRoom.LobbyRoomClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                Int32Value createdMatchId = lobbyRoomClient.StartMatch(new Empty(), requestHeaders);
                return createdMatchId.Value;
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                throw new InvalidOperationException(rpcException.Status.Detail, rpcException);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while starting match.");
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public List<FriendSummary> GetFriends()
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                LobbyRoom.LobbyRoomClient lobbyRoomClient = new LobbyRoom.LobbyRoomClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                FriendSummaryList friendSummaryList = lobbyRoomClient.GetFriends(new Empty(), requestHeaders);

                List<FriendSummary> friends = new List<FriendSummary>();
                foreach (FriendSummary friend in friendSummaryList.Friends)
                {
                    friends.Add(friend);
                }

                return friends;
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while loading friends.");
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public void InviteFriend(int friendUserId)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                LobbyRoom.LobbyRoomClient lobbyRoomClient = new LobbyRoom.LobbyRoomClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                lobbyRoomClient.InviteFriend(new Int32Value { Value = friendUserId }, requestHeaders);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while inviting friend {0}.", friendUserId);
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public void SendMessage(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Chat.ChatClient chatClient = new Chat.ChatClient(grpcChannel);
                Metadata requestHeaders = BuildPlayerHeaders();
                ChatMessage outgoingMessage = new ChatMessage
                {
                    Scope = ChatScope.Lobby,
                    Text = messageText.Trim()
                };

                chatClient.SendMessage(outgoingMessage, requestHeaders);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while sending lobby chat message.");
                throw new DataOperationException(Strings.LobbyErrorConnectionFailed, rpcException);
            }
        }

        public void SubscribeToEvents(ILobbyCallback lobbyCallback)
        {
            StopSubscription();
            streamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = streamCancellation.Token;
            Task.Run(() =>
            {
                try
                {
                    GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                    Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                    Metadata requestHeaders = BuildPlayerHeaders();

                    using AsyncServerStreamingCall<LobbyEvent> streamingCall = lobbyClient.SubscribeLobbyEvents(
                        new Empty(),
                        requestHeaders,
                        cancellationToken: cancellationToken);

                    while (streamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                    {
                        LobbyEvent currentLobbyEvent = streamingCall.ResponseStream.Current;
                        DispatchLobbyEvent(currentLobbyEvent, lobbyCallback);
                    }
                }
                catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.Cancelled)
                {
                    JsonLogger.Error(rpcException, "Lobby event stream cancelled by client.");
                }
                catch (OperationCanceledException canceledException)
                {
                    JsonLogger.Error(canceledException, "Lobby event stream subscription operation cancelled.");
                }
                catch (RpcException rpcException)
                {
                    JsonLogger.Error(rpcException, "Lobby event stream disconnected with RpcException.");
                }
            });
        }

        public void SubscribeChat(IChatCallback chatCallback)
        {
            StopChatSubscription();
            chatStreamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = chatStreamCancellation.Token;
            Task.Run(() =>
            {
                try
                {
                    GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                    Chat.ChatClient chatClient = new Chat.ChatClient(grpcChannel);
                    Metadata requestHeaders = BuildPlayerHeaders();
                    ChatChannel channelRequest = new ChatChannel
                    {
                        Scope = ChatScope.Lobby
                    };

                    using AsyncServerStreamingCall<ChatMessage> chatStreamingCall = chatClient.SubscribeChat(
                        channelRequest,
                        requestHeaders,
                        cancellationToken: cancellationToken);

                    while (chatStreamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                    {
                        ChatMessage incomingMessage = chatStreamingCall.ResponseStream.Current;
                        chatCallback.MessageReceived(incomingMessage);
                    }
                }
                catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.Cancelled)
                {
                    JsonLogger.Error(rpcException, "Chat stream cancelled by client.");
                }
                catch (OperationCanceledException canceledException)
                {
                    JsonLogger.Error(canceledException, "Chat stream subscription operation cancelled.");
                }
                catch (RpcException rpcException)
                {
                    JsonLogger.Error(rpcException, "Chat stream disconnected with RpcException.");
                }
            });
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
            switch (currentLobbyEvent.PayloadCase)
            {
                case LobbyEvent.PayloadOneofCase.LobbyChanged:
                    lobbyCallback.LobbyChanged(currentLobbyEvent.LobbyChanged);
                    break;
                case LobbyEvent.PayloadOneofCase.LobbyClosed:
                    lobbyCallback.LobbyClosed(currentLobbyEvent.LobbyClosed);
                    break;
                case LobbyEvent.PayloadOneofCase.MatchStarting:
                    lobbyCallback.MatchStarting(currentLobbyEvent.MatchStarting);
                    break;
                default:
                    break;
            }
        }

        private static Metadata BuildPlayerHeaders()
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            bool isGuestPlayer = currentSessionUser.IdUser < 0;
            string avatarReference = string.Empty;
            if (currentSessionUser.AvatarReference != null)
            {
                avatarReference = currentSessionUser.AvatarReference;
            }

            return new Metadata
            {
                { UserIdHeaderKey, currentSessionUser.IdUser.ToString() },
                { NicknameHeaderKey, currentSessionUser.Nickname },
                { AvatarHeaderKey, avatarReference },
                { IsGuestHeaderKey, isGuestPlayer.ToString() }
            };
        }
    }
}
