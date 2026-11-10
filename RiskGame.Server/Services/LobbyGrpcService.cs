using System;
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
        private const string DefaultGuestNickname = "Guest";
        private readonly LobbySessionManager sessionManager;

        public LobbyGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<LobbyDetails> CreateLobby(CreateLobbyRequest createRequest, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary hostPlayer = EnsureValidPlayer(createRequest.Player);
                LobbyDetails createdLobbyDetails = sessionManager.CreateLobby(createRequest.Rules, hostPlayer);
                return Task.FromResult(createdLobbyDetails);
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<LobbyDetails> JoinLobby(JoinLobbyRequest joinRequest, ServerCallContext callContext)
        {
            try
            {
                PlayerSummary joiningPlayer = EnsureValidPlayer(joinRequest.Player);
                LobbyDetails lobbyDetails = sessionManager.JoinLobby(joinRequest.LobbyCode, joiningPlayer);
                return Task.FromResult(lobbyDetails);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
            catch (LobbyFullException lobbyFullException)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, lobbyFullException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<Empty> LeaveLobby(UserLobbyRequest leaveRequest, ServerCallContext callContext)
        {
            sessionManager.LeaveLobby(leaveRequest.UserId);
            return Task.FromResult(new Empty());
        }

        public override Task<StartMatchResponse> StartMatch(UserLobbyRequest startRequest, ServerCallContext callContext)
        {
            try
            {
                int createdMatchId = sessionManager.StartMatch(startRequest.UserId);
                StartMatchResponse startResponse = new StartMatchResponse
                {
                    MatchId = createdMatchId
                };
                return Task.FromResult(startResponse);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
            catch (InvalidOperationException invalidOperationException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, invalidOperationException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<FriendSummaryList> GetFriends(UserLobbyRequest friendsRequest, ServerCallContext callContext)
        {
            try
            {
                FriendSummaryList friends = sessionManager.GetFriends(friendsRequest.UserId);
                return Task.FromResult(friends);
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<Empty> InviteFriend(InviteFriendRequest inviteRequest, ServerCallContext callContext)
        {
            try
            {
                sessionManager.InviteFriend(inviteRequest.SenderUserId, inviteRequest.RecipientUserId);
                return Task.FromResult(new Empty());
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task SubscribeLobbyEvents(
            UserLobbyRequest subscribeRequest,
            IServerStreamWriter<LobbyEvent> responseStream,
            ServerCallContext callContext)
        {
            try
            {
                LobbyDetails initialLobbySnapshot = sessionManager.RegisterSubscriber(subscribeRequest.UserId, responseStream);

                LobbyEvent snapshotEvent = new LobbyEvent
                {
                    Lobby = initialLobbySnapshot
                };

                lock (responseStream)
                {
                    responseStream.WriteAsync(snapshotEvent).GetAwaiter().GetResult();
                }

                callContext.CancellationToken.WaitHandle.WaitOne();
                sessionManager.UnregisterSubscriber(subscribeRequest.UserId);
                return Task.CompletedTask;
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
        }

        private static PlayerSummary EnsureValidPlayer(PlayerSummary? requestedPlayer)
        {
            if (requestedPlayer != null && !string.IsNullOrWhiteSpace(requestedPlayer.Nickname))
            {
                return requestedPlayer;
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
