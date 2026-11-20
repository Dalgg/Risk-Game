namespace RiskGame.Server.Services
{
    public interface ICompletedMatchRepository
    {
        void SaveCompletedMatch(ActiveMatch match);
    }
}
