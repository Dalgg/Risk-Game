using System;

namespace RiskGame.Server.Services
{
    public sealed class ReportSessionManager
    {
        private const int SuspensionHours = 24;
        private const int RequiredDistinctReporters = 3;
        private readonly LobbySessionManager lobbies;
        private readonly PlayerReportRepositoryService repository;
        private readonly TimeProvider timeProvider;

        public ReportSessionManager(LobbySessionManager lobbies, PlayerReportRepositoryService repository,
            TimeProvider timeProvider)
        {
            this.lobbies = lobbies;
            this.repository = repository;
            this.timeProvider = timeProvider;
        }

        public bool ReportPlayer(PlayerReportCommand command)
        {
            lobbies.ValidateParticipant(command.UserId, command.SessionToken);
            DateTime reportDate = timeProvider.GetUtcNow().UtcDateTime;
            PlayerReportSubmission submission = new PlayerReportSubmission
            {
                ReporterId = command.UserId,
                ReportedId = command.ReportedUserId,
                LobbyId = command.LobbyId,
                ReportDateUtc = reportDate,
                BannedUntilUtc = reportDate.AddHours(SuspensionHours),
                RequiredDistinctReporters = RequiredDistinctReporters
            };
            return lobbies.RecordPlayerReport(submission, repository).IsDuplicate;
        }
    }
}
