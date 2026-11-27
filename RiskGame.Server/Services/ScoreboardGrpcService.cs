using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Scoreboard;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class ScoreboardGrpcService : Scoreboard.ScoreboardBase
    {
        private readonly GrpcSessionAuthenticator authenticator;
        private readonly IScoreboardRepository scoreboardRepository;

        public ScoreboardGrpcService(GrpcSessionAuthenticator authenticator, IScoreboardRepository repository)
        {
            this.authenticator = authenticator;
            this.scoreboardRepository = repository;
        }

        public override Task<ScoreList> GetScores(ScoreQuery request, ServerCallContext callContext)
        {
            try
            {
                int userId = authenticator.GetRegisteredUserId(request.UserId, request.SessionToken);
                ScoreboardFilter filter = new ScoreboardFilter(request, userId);
                return Task.FromResult(BuildScores(filter));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFailure(dataOperationException);
            }
        }

        private ScoreList BuildScores(ScoreboardFilter filter)
        {
            ScoreList scoreList = new ScoreList();
            foreach (ServerScoreEntry entry in scoreboardRepository.GetScores(filter))
            {
                scoreList.Players.Add(new ScoreEntry
                {
                    Username = entry.Username,
                    TotalWins = entry.TotalWins,
                    TotalScore = entry.TotalScore
                });
            }
            return scoreList;
        }

        private RpcException CreateFailure(DataOperationException dataOperationException)
        {
            StatusCode status = StatusCode.Unavailable;
            if (dataOperationException is InvalidSessionException)
            {
                status = StatusCode.Unauthenticated;
            }
            else if (dataOperationException is GuestNotAllowedException)
            {
                status = StatusCode.PermissionDenied;
            }
            else
            {
                JsonLogger.Error(dataOperationException, "Could not query the scoreboard database.");
            }
            return new RpcException(new Status(status, "The scoreboard request could not be completed."));
        }
    }
}
