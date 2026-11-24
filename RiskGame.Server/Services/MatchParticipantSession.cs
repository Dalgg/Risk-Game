namespace RiskGame.Server.Services
{
    public sealed record MatchParticipantSession(int MatchId, int UserId, string SessionToken);
}
