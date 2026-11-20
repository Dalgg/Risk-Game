using System.Collections.Generic;

namespace RiskGame.Server.Services
{
    public interface IScoreboardRepository
    {
        IReadOnlyList<ServerScoreEntry> GetScores(ScoreboardFilter filter);
    }
}
