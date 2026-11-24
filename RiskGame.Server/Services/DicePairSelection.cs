namespace RiskGame.Server.Services
{
    public sealed record DicePairSelection(long RollId, int PairIndex, int PreferredColumn = 0);
}
