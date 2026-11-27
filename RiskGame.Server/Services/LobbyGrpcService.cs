using System;
using System.Threading;
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
        private const int StreamSessionCheckSeconds = 1;
        private const int InvalidUserIdentifier = 0;
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
                ValidateParticipant(hostPlayer.UserId, createRequest.SessionToken);
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
                ValidateParticipant(joiningPlayer.UserId, joinRequest.SessionToken);
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
                MatchParticipantSession participant = new MatchParticipantSession(
                    request.MatchId, request.UserId, request.SessionToken);
                return Task.FromResult(sessionManager.ReturnToLobby(participant));
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
            ValidateParticipant(leaveRequest.UserId, leaveRequest.SessionToken);
            sessionManager.LeaveLobby(leaveRequest.UserId);
            return Task.FromResult(new Empty());
        }

        public override Task<StartMatchResponse> StartMatch(UserLobbyRequest startRequest,
            ServerCallContext callContext)
        {
            try
            {
                ValidateParticipant(startRequest.UserId, startRequest.SessionToken);
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

        public override Task<FriendSummaryList> GetFriends(UserLobbyRequest friendsRequest,
            ServerCallContext callContext)
        {
            try
            {
                ValidateParticipant(friendsRequest.UserId, friendsRequest.SessionToken);
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
                ValidateParticipant(inviteRequest.SenderUserId, inviteRequest.SessionToken);
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
                return ReadLobbySession(subscribeRequest, responseStream, callContext.CancellationToken);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
        }

        private static PlayerSummary EnsureValidPlayer(PlayerSummary? requestedPlayer)
        {
            if (requestedPlayer == null || requestedPlayer.UserId == InvalidUserIdentifier
                || string.IsNullOrWhiteSpace(requestedPlayer.Nickname))
            {
                throw new ArgumentException("A valid player is required.");
            }
            PlayerSummary player = requestedPlayer.Clone();
            player.IsGuest = player.UserId < InvalidUserIdentifier;
            return player;
        }

        private void ValidateParticipant(int userId, string sessionToken)
        {
            try
            {
                sessionManager.ValidateParticipant(userId, sessionToken);
            }
            catch (InvalidSessionException invalidSessionException)
            {
                JsonLogger.Error(invalidSessionException, "The lobby operation requires a valid player session.");
                throw new RpcException(new Status(StatusCode.Unauthenticated, invalidSessionException.Message));
            }
        }

        private Task ReadLobbySession(UserLobbyRequest request, IServerStreamWriter<LobbyEvent> stream,
            CancellationToken cancellationToken)
        {
            ValidateParticipant(request.UserId, request.SessionToken);
            try
            {
                SendInitialLobby(request.UserId, stream, cancellationToken);
                WaitForLobbySession(request, stream, cancellationToken);
                return Task.CompletedTask;
            }
            finally
            {
                sessionManager.UnregisterSubscriber(request.UserId, stream);
            }
        }

        private void SendInitialLobby(int userId, IServerStreamWriter<LobbyEvent> stream,
            CancellationToken cancellationToken)
        {
            LobbyDetails details = sessionManager.RegisterSubscriber(userId, stream);
            LobbyEvent initial = new LobbyEvent { Lobby = details };
            lock (stream)
            {
                stream.WriteAsync(initial, cancellationToken).GetAwaiter().GetResult();
            }
        }

        private void WaitForLobbySession(UserLobbyRequest request, IServerStreamWriter<LobbyEvent> stream,
            CancellationToken cancellationToken)
        {
            TimeSpan interval = TimeSpan.FromSeconds(StreamSessionCheckSeconds);
            bool current = true;
            while (current && !cancellationToken.WaitHandle.WaitOne(interval))
            {
                ValidateParticipant(request.UserId, request.SessionToken);
                current = sessionManager.IsLobbySubscriberCurrent(request.UserId, stream);
            }
        }
    }
}
