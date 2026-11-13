using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Callbacks;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed class MatchViewModel : ViewModelBase
    {
        private const int ActionTimeoutSeconds = 10;
        private CancellationTokenSource? streamCancellation;

        public BoardState RollDice(int matchId, long expectedRollId)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            RollDiceRequest request = new RollDiceRequest
            {
                MatchId = matchId, UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken, ExpectedRollId = expectedRollId
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                return matchClient.RollDice(request, deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not roll dice for the current match.");
                throw new DataOperationException(GetActionError(rpcException), rpcException);
            }
        }

        public BoardState ChooseDicePair(int matchId, long rollId, int pairIndex)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            PairChoice request = new PairChoice
            {
                MatchId = matchId, UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken, RollId = rollId, PairIndex = pairIndex
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                return matchClient.ChooseDicePair(request, deadline: DateTime.UtcNow.AddSeconds(ActionTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not choose a dice pair for the current match.");
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
            streamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = streamCancellation.Token;
            Task.Run(() => ReadMatchStream(subscription, matchCallback, cancellationToken));
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

        private void ReadMatchStream(MatchSubscription subscription, IMatchCallback matchCallback,
            CancellationToken cancellationToken)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Match.MatchClient matchClient = new Match.MatchClient(grpcChannel);
                using AsyncServerStreamingCall<BoardState> streamingCall = matchClient.SubscribeMatchEvents(
                    subscription, cancellationToken: cancellationToken);
                while (streamingCall.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult())
                {
                    matchCallback.MatchUpdated(streamingCall.ResponseStream.Current);
                }
                if (!cancellationToken.IsCancellationRequested)
                {
                    matchCallback.MatchStreamStopped(Strings.MatchConnectionError);
                }
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "The match stream stopped with a gRPC error.");
                if (!cancellationToken.IsCancellationRequested)
                {
                    matchCallback.MatchStreamStopped(GetStreamError(rpcException));
                }
            }
            catch (OperationCanceledException canceledException)
            {
                JsonLogger.Error(canceledException, "The match subscription was cancelled.");
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
            return errorMessage;
        }
    }
}
