using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Callbacks;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed class MatchViewModel : ViewModelBase
    {
        private const int ActionTimeoutSeconds = 10;
        private const int LeaveTimeoutSeconds = 3;
        private readonly object subscriptionLock = new object();
        private readonly SessionUser activeSession = SessionService.GetOrCreateCurrentUser();
        private CancellationTokenSource? streamCancellation;

        public MatchCountdownViewModel Countdown { get; } = new MatchCountdownViewModel();

        public BoardState StopTurn(int matchId, long turnId)
        {
            SessionUser session = SessionService.GetOrCreateCurrentUser();
            StopTurnRequest request = new StopTurnRequest
            {
                MatchId = matchId, UserId = session.IdUser, SessionToken = session.SessionToken, TurnId = turnId
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                return matchClient.StopTurn(
                    request,
                    deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds),
                    cancellationToken: CancellationToken.None);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(GetActionError(rpcException), rpcException);
            }
        }

        public BoardState RollDice(int matchId, long expectedRollId, long turnId)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            RollDiceRequest request = new RollDiceRequest
            {
                MatchId = matchId, UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken, ExpectedRollId = expectedRollId, TurnId = turnId
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                return matchClient.RollDice(
                    request,
                    deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds),
                    cancellationToken: CancellationToken.None);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(GetActionError(rpcException), rpcException);
            }
        }

        public BoardState ChooseDicePair(int matchId, PairChoice request)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            request.MatchId = matchId;
            request.UserId = currentSessionUser.IdUser;
            request.SessionToken = currentSessionUser.SessionToken;
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                return matchClient.ChooseDicePair(
                    request,
                    deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds),
                    cancellationToken: CancellationToken.None);
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(GetActionError(rpcException), rpcException);
            }
        }

        private string GetActionError(RpcException rpcException)
        {
            if (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                return Strings.MatchActionRejected;
            }
            return GetStreamError(rpcException);
        }

        public void Subscribe(int matchId, IMatchCallback matchCallback)
        {
            StopSubscription();
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            MatchSubscription subscription = new MatchSubscription
            {
                MatchId = matchId,
                UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken
            };
            CancellationTokenSource cancellation = new CancellationTokenSource();
            lock (subscriptionLock)
            {
                streamCancellation = cancellation;
            }
            Task.Run(() => ReadMatchStream(subscription, matchCallback, cancellation));
        }

        public void StopSubscription()
        {
            lock (subscriptionLock)
            {
                if (streamCancellation != null)
                {
                    streamCancellation.Cancel();
                    streamCancellation = null;
                }
            }
        }

        public void LeaveMatch(int matchId)
        {
            MatchSubscription request = new MatchSubscription
            {
                MatchId = matchId, UserId = activeSession.IdUser, SessionToken = activeSession.SessionToken
            };
            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                Match.MatchClient client = new Match.MatchClient(channel);
                client.LeaveMatch(request, deadline: DateTime.UtcNow.AddSeconds(LeaveTimeoutSeconds));
                StopSubscription();
                LeaveLobby();
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.MatchLeaveError, rpcException);
            }
        }

        public void LeaveLobby()
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                UserLobbyRequest leaveRequest = new UserLobbyRequest { UserId = activeSession.IdUser };
                lobbyClient.LeaveLobby(
                    leaveRequest,
                    deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds),
                    cancellationToken: CancellationToken.None);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not leave the lobby when closing the match window.");
            }
        }

        private void ReadMatchStream(MatchSubscription subscription, IMatchCallback matchCallback,
            CancellationTokenSource cancellation)
        {
            CancellationToken cancellationToken = cancellation.Token;
            bool matchFinished = false;
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                using AsyncServerStreamingCall<BoardState> streamingCall = matchClient.SubscribeMatchEvents(
                    subscription, cancellationToken: cancellationToken);
                while (!matchFinished
                    && streamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        BoardState state = streamingCall.ResponseStream.Current;
                        matchFinished = state.IsFinished;
                        matchCallback.MatchUpdated(state);
                    }
                }
                if (!cancellationToken.IsCancellationRequested && !matchFinished)
                {
                    matchCallback.MatchStreamStopped(Strings.MatchConnectionError);
                }
            }
            catch (RpcException rpcException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    JsonLogger.Error(rpcException, "The match stream stopped with a gRPC error.");
                    matchCallback.MatchStreamStopped(GetStreamError(rpcException));
                }
            }
            catch (OperationCanceledException canceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    JsonLogger.Error(canceledException, "The match subscription was cancelled unexpectedly.");
                    matchCallback.MatchStreamStopped(Strings.MatchConnectionError);
                }
            }
            finally
            {
                cancellation.Cancel();
                lock (subscriptionLock)
                {
                    if (ReferenceEquals(streamCancellation, cancellation))
                    {
                        streamCancellation = null;
                    }
                    cancellation.Dispose();
                }
            }
        }

        private string GetStreamError(RpcException rpcException)
        {
            string errorMessage = Strings.MatchConnectionError;
            if (rpcException.StatusCode == StatusCode.NotFound)
            {
                errorMessage = Strings.MatchUnavailable;
            }
            if (rpcException.StatusCode == StatusCode.Unauthenticated)
            {
                errorMessage = Strings.MatchAccessDenied;
            }
            if (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                errorMessage = Strings.MatchReconnectUnavailable;
            }
            return errorMessage;
        }
    }
}
