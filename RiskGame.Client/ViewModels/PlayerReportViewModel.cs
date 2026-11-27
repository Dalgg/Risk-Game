using System;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Report;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed class PlayerReportViewModel : ViewModelBase
    {
        private const int MinimumRegisteredIdentifier = 1;
        private const int RequestTimeoutSeconds = 10;

        public static bool CanReport(LobbyPlayer targetPlayer, LobbyStatus lobbyStatus)
        {
            SessionUser? session = SessionService.CurrentUser;
            bool allowed = false;
            if (session != null && targetPlayer.Player != null)
            {
                bool isRegisteredReporter = !session.IsGuest;
                bool isRegisteredTarget = !targetPlayer.Player.IsGuest
                    && targetPlayer.Player.UserId >= MinimumRegisteredIdentifier;
                bool isDifferentAccount = targetPlayer.Player.UserId != session.IdUser;
                bool canSubmitReport = isRegisteredReporter && lobbyStatus == LobbyStatus.Waiting;
                bool canReportTarget = isRegisteredTarget && isDifferentAccount;
                allowed = canSubmitReport && canReportTarget;
            }
            return allowed;
        }

        public bool ReportPlayer(int lobbyId, int reportedUserId)
        {
            SessionUser session = SessionService.GetOrCreateCurrentUser();
            if (session.IsGuest || reportedUserId < MinimumRegisteredIdentifier || reportedUserId == session.IdUser)
            {
                throw new DataOperationException(Strings.PlayerReportNotAllowed);
            }
            PlayerReportRequest request = new PlayerReportRequest
            {
                UserId = session.IdUser, SessionToken = session.SessionToken,
                ReportedUserId = reportedUserId, LobbyId = lobbyId
            };
            return SendReport(request).IsDuplicate;
        }

        private static PlayerReportResponse SendReport(PlayerReportRequest request)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                PlayerReport.PlayerReportClient reportClient = new PlayerReport.PlayerReportClient(grpcChannel);
                return reportClient.ReportPlayer(request, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(GetReportError(rpcException.StatusCode), rpcException);
            }
        }

        private static string GetReportError(StatusCode statusCode)
        {
            string message = Strings.PlayerReportConnectionError;
            switch (statusCode)
            {
                case StatusCode.InvalidArgument:
                case StatusCode.FailedPrecondition:
                case StatusCode.NotFound:
                case StatusCode.PermissionDenied:
                case StatusCode.Unauthenticated:
                    message = Strings.PlayerReportNotAllowed;
                    break;
                default:
                    break;
            }
            return message;
        }
    }
}
