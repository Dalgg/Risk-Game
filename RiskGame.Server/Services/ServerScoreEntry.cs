namespace RiskGame.Server.Services
{
    public sealed class ServerScoreEntry
    {
        public int UserId { get; init; }

        public string Username { get; init; } = string.Empty;

        public int TotalScore { get; init; }

        public int TotalWins { get; init; }

    }
}
