using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class MatchGrpcService : Match.MatchBase
    {
        private readonly MatchSessionManager matchSessionManager;

        public MatchGrpcService(MatchSessionManager matchSessionManager)
        {
            this.matchSessionManager = matchSessionManager;
        }

        public override Task SubscribeMatchEvents(MatchSubscription subscription,
            IServerStreamWriter<BoardState> responseStream, ServerCallContext callContext)
        {
            try
            {
                return matchSessionManager.SubscribeMatchEvents(subscription, responseStream,
                    callContext.CancellationToken);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not subscribe to the requested match.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<BoardState> RollDice(RollDiceRequest request, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(matchSessionManager.RollDice(request));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not roll dice for the requested match.");
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<BoardState> ChooseDicePair(PairChoice request, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(matchSessionManager.ChooseDicePair(request));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not choose a pair for the requested match.");
                throw CreateRpcException(dataOperationException);
            }
        }

        private RpcException CreateRpcException(DataOperationException dataOperationException)
        {
            StatusCode statusCode = StatusCode.Unauthenticated;
            if (dataOperationException is MatchNotFoundException)
            {
                statusCode = StatusCode.NotFound;
            }
            if (dataOperationException is MatchActionException)
            {
                statusCode = StatusCode.FailedPrecondition;
            }
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }
    }
}
