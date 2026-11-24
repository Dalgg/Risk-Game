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
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<BoardState> StopTurn(StopTurnRequest request, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(matchSessionManager.StopTurn(request));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateRpcException(dataOperationException);
            }
        }

        public override Task<BoardState> LeaveMatch(MatchSubscription request, ServerCallContext context)
        {
            try
            {
                return Task.FromResult(matchSessionManager.LeaveMatch(request));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateRpcException(dataOperationException);
            }
        }

        private RpcException CreateRpcException(DataOperationException dataOperationException)
        {
            LogTechnicalFailure(dataOperationException);
            StatusCode statusCode = StatusCode.Unavailable;
            if (dataOperationException is InvalidSessionException)
            {
                statusCode = StatusCode.Unauthenticated;
            }
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

        private void LogTechnicalFailure(DataOperationException dataOperationException)
        {
            if (dataOperationException.GetType() == typeof(DataOperationException))
            {
                JsonLogger.Error(dataOperationException, "The match operation failed due to an infrastructure error.");
            }
        }
    }
}
