using System;

namespace RiskGame.Server.Services
{
    public sealed class PlayerReportResult
    {
        public bool IsDuplicate { get; init; }

        public DateTime? BannedUntil { get; init; }
    }
}
