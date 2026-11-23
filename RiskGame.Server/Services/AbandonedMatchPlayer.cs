namespace RiskGame.Server.Services
{
    public sealed record AbandonedMatchPlayer(int MatchId, int LobbyId, int UserId);
}
