namespace RiskGame.Server.Services
{
    public sealed class PlayerReportCommand
    {
        public int UserId { get; init; }

        public string SessionToken { get; init; } = string.Empty;

        public int ReportedUserId { get; init; }

        public int LobbyId { get; init; }
    }
}
