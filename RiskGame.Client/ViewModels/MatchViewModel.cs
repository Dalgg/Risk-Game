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
        private const int ConnectionTimeoutSeconds = 10;
        private readonly object subscriptionLock = new object();
        private readonly SessionUser activeSession = SessionService.GetOrCreateCurrentUser();
        private readonly Func<MatchSubscription, CancellationToken, AsyncServerStreamingCall<BoardState>>
            openMatchStream;
        private readonly TimeSpan connectionTimeout;
        private CancellationTokenSource? streamCancellation;
        private bool isConnecting;

        public MatchViewModel() : this(OpenMatchStream, TimeSpan.FromSeconds(ConnectionTimeoutSeconds))
        {
        }

        public MatchViewModel(
            Func<MatchSubscription, CancellationToken, AsyncServerStreamingCall<BoardState>> openMatchStream,
            TimeSpan connectionTimeout)
        {
            this.openMatchStream = openMatchStream;
            this.connectionTimeout = connectionTimeout;
        }

        public bool IsConnecting
        {
            get
            {
                lock (subscriptionLock)
                {
                    return isConnecting;
                }
            }
        }

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
                throw CreateActionException(rpcException);
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
                throw CreateActionException(rpcException);
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
                throw CreateActionException(rpcException);
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

        internal DataOperationException CreateActionException(RpcException rpcException)
        {
            DataOperationException actionException =
                new DataOperationException(GetActionError(rpcException), rpcException);
            bool isCommunicationFailure = rpcException.StatusCode == StatusCode.Unavailable
                || rpcException.StatusCode == StatusCode.DeadlineExceeded;
            if (isCommunicationFailure)
            {
                StopSubscription();
                actionException = new MatchConnectionException(Strings.MatchConnectionError, rpcException);
            }
            return actionException;
        }

        public bool Subscribe(int matchId, IMatchCallback matchCallback)
        {
            bool started = false;
            lock (subscriptionLock)
            {
                if (!isConnecting && streamCancellation == null)
                {
                    SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
                    MatchSubscription subscription = new MatchSubscription
                    {
                        MatchId = matchId,
                        UserId = currentSessionUser.IdUser,
                        SessionToken = currentSessionUser.SessionToken
                    };
                    CancellationTokenSource cancellation = new CancellationTokenSource();
                    streamCancellation = cancellation;
                    isConnecting = true;
                    Task.Run(() => ReadMatchStream(subscription, matchCallback, cancellation));
                    started = true;
                }
            }
            return started;
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
                isConnecting = false;
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
                client.LeaveMatch(
                    request,
                    deadline: DateTime.UtcNow.AddSeconds(LeaveTimeoutSeconds),
                    cancellationToken: CancellationToken.None);
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
                UserLobbyRequest leaveRequest = new UserLobbyRequest
                {
                    UserId = activeSession.IdUser, SessionToken = activeSession.SessionToken
                };
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
            string errorMessage = string.Empty;
            try
            {
                using AsyncServerStreamingCall<BoardState> streamingCall =
                    openMatchStream(subscription, cancellationToken);
                errorMessage = PumpStreamUpdates(streamingCall, matchCallback, cancellation);
            }
            catch (RpcException rpcException)
            {
                errorMessage = HandleStreamException(
                    rpcException, GetStreamError(rpcException),
                    "The match stream disconnected for match {0}, player {1}.",
                    subscription, cancellationToken);
            }
            catch (OperationCanceledException canceledException)
            {
                errorMessage = HandleStreamException(
                    canceledException, Strings.MatchConnectionError,
                    "The match subscription was cancelled unexpectedly for match {0}, player {1}.",
                    subscription, cancellationToken);
            }
            catch (TimeoutException timeoutException)
            {
                errorMessage = HandleStreamException(
                    timeoutException, Strings.MatchConnectionError,
                    "Connection timeout for match {0}, player {1}.",
                    subscription, cancellationToken);
            }
            catch (Exception exception)
            {
                errorMessage = HandleStreamException(
                    exception, Strings.MatchConnectionError,
                    "Could not read the stream for match {0}, player {1}.",
                    subscription, cancellationToken);
            }
            finally
            {
                CompleteStreamShutdown(cancellation, errorMessage, matchCallback);
            }
        }

        private string PumpStreamUpdates(AsyncServerStreamingCall<BoardState> streamingCall,
            IMatchCallback matchCallback, CancellationTokenSource cancellation)
        {
            CancellationToken cancellationToken = cancellation.Token;
            bool matchFinished = false;
            bool hasNext = streamingCall.ResponseStream.MoveNext(cancellationToken)
                .WaitAsync(connectionTimeout, cancellationToken).GetAwaiter().GetResult();
            while (!matchFinished && hasNext)
            {
                if (ConfirmSubscription(cancellation))
                {
                    BoardState state = streamingCall.ResponseStream.Current;
                    matchFinished = state.IsFinished;
                    matchCallback.MatchUpdated(state);
                }
                if (!matchFinished)
                {
                    hasNext = streamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult();
                }
            }
            bool isExpectedTermination = cancellationToken.IsCancellationRequested || matchFinished;
            if (!isExpectedTermination)
            {
                return Strings.MatchConnectionError;
            }
            return string.Empty;
        }

        private static string HandleStreamException(Exception exception, string userMessage,
            string logTemplate, MatchSubscription subscription, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return string.Empty;
            }
            JsonLogger.Error(exception, logTemplate, subscription.MatchId, subscription.UserId);
            return userMessage;
        }

        private void CompleteStreamShutdown(CancellationTokenSource cancellation, string errorMessage,
            IMatchCallback matchCallback)
        {
            bool notifyFailure = false;
            lock (subscriptionLock)
            {
                if (ReferenceEquals(streamCancellation, cancellation))
                {
                    notifyFailure = !cancellation.IsCancellationRequested && !string.IsNullOrEmpty(errorMessage);
                    streamCancellation = null;
                    isConnecting = false;
                }
                cancellation.Cancel();
                cancellation.Dispose();
            }
            if (notifyFailure)
            {
                matchCallback.MatchStreamStopped(errorMessage);
            }
        }

        private bool ConfirmSubscription(CancellationTokenSource cancellation)
        {
            lock (subscriptionLock)
            {
                bool isCurrent = ReferenceEquals(streamCancellation, cancellation)
                    && !cancellation.IsCancellationRequested;
                if (isCurrent)
                {
                    isConnecting = false;
                }
                return isCurrent;
            }
        }

        private static AsyncServerStreamingCall<BoardState> OpenMatchStream(
            MatchSubscription subscription, CancellationToken cancellationToken)
        {
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
            return matchClient.SubscribeMatchEvents(subscription, cancellationToken: cancellationToken);
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
