using System;

namespace RiskGame.Server.Services
{
    public sealed class PlayerReportSubmission
    {
        public int ReporterId { get; init; }

        public int ReportedId { get; init; }

        public int LobbyId { get; init; }

        public DateTime ReportDateUtc { get; init; }

        public DateTime BannedUntilUtc { get; init; }

        public int RequiredDistinctReporters { get; init; }
    }
}
