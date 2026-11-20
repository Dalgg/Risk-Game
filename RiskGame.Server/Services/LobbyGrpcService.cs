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
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not create the requested lobby.");
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
                JsonLogger.Error(dataOperationException, "Could not join the requested lobby.");
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<LobbyDetails> ReturnToLobby(ReturnToLobbyRequest request, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(sessionManager.ReturnToLobby(request));
            }
            catch (MatchNotFoundException matchNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, matchNotFoundException.Message));
            }
            catch (InvalidSessionException invalidSessionException)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, invalidSessionException.Message));
            }
            catch (MatchActionException matchActionException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, matchActionException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not return to the original lobby.");
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
                JsonLogger.Error(dataOperationException, "Could not start the match due to a persistence failure.");
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
                JsonLogger.Error(dataOperationException, "Could not retrieve the player's friends list.");
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
                JsonLogger.Error(dataOperationException, "Could not persist the lobby invitation.");
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
                    responseStream.WriteAsync(snapshotEvent, callContext.CancellationToken).GetAwaiter().GetResult();
                }

                callContext.CancellationToken.WaitHandle.WaitOne();
                sessionManager.UnregisterSubscriber(subscribeRequest.UserId, responseStream);
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
