using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Report;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class PlayerReportGrpcService : PlayerReport.PlayerReportBase
    {
        private readonly ReportSessionManager reports;

        public PlayerReportGrpcService(ReportSessionManager reports)
        {
            this.reports = reports;
        }

        public override Task<PlayerReportResponse> ReportPlayer(PlayerReportRequest request,
            ServerCallContext callContext)
        {
            try
            {
                PlayerReportCommand command = CreateCommand(request);
                return Task.FromResult(new PlayerReportResponse { IsDuplicate = reports.ReportPlayer(command) });
            }
            catch (DataOperationException dataOperationException)
            {
                throw TranslateFailure(dataOperationException);
            }
        }

        private static PlayerReportCommand CreateCommand(PlayerReportRequest request)
        {
            return new PlayerReportCommand
            {
                UserId = request.UserId,
                SessionToken = request.SessionToken,
                ReportedUserId = request.ReportedUserId,
                LobbyId = request.LobbyId
            };
        }

        private static RpcException TranslateFailure(DataOperationException dataOperationException)
        {
            StatusCode statusCode = StatusCode.Unavailable;
            if (dataOperationException is InvalidSessionException)
            {
                statusCode = StatusCode.Unauthenticated;
            }
            else if (dataOperationException is ReportNotAllowedException)
            {
                statusCode = StatusCode.FailedPrecondition;
            }
            statusCode = ResolveDataFailure(dataOperationException, statusCode);
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }

        private static StatusCode ResolveDataFailure(DataOperationException dataOperationException, StatusCode statusCode)
        {
            if (dataOperationException is ProfileNotFoundException)
            {
                statusCode = StatusCode.NotFound;
            }
            if (statusCode == StatusCode.Unavailable)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the player report.");
            }
            return statusCode;
        }
    }
}
