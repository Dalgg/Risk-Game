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
                MatchParticipantSession participant = new MatchParticipantSession(
                    subscription.MatchId, subscription.UserId, subscription.SessionToken);
                return matchSessionManager.SubscribeMatchEvents(participant, responseStream,
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
                MatchParticipantSession participant = new MatchParticipantSession(
                    request.MatchId, request.UserId, request.SessionToken);
                return Task.FromResult(
                    matchSessionManager.RollDice(participant, request.TurnId, request.ExpectedRollId));
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
                MatchParticipantSession participant = new MatchParticipantSession(
                    request.MatchId, request.UserId, request.SessionToken);
                DicePairSelection selection = new DicePairSelection(
                    request.RollId, request.PairIndex, request.PreferredColumn);
                return Task.FromResult(matchSessionManager.ChooseDicePair(participant, request.TurnId, selection));
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
                MatchParticipantSession participant = new MatchParticipantSession(
                    request.MatchId, request.UserId, request.SessionToken);
                return Task.FromResult(matchSessionManager.StopTurn(participant, request.TurnId));
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
                MatchParticipantSession participant = new MatchParticipantSession(
                    request.MatchId, request.UserId, request.SessionToken);
                return Task.FromResult(matchSessionManager.LeaveMatch(participant));
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
