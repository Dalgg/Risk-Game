using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed record ServerScoreEntry
    {
        public int Rank { get; init; }

        public string Username { get; init; } = string.Empty;

        public int TotalScore { get; init; }
    }

    public class ScoreboardRepositoryService
    {
        private const int DefaultTopPlayersLimit = 10;

        public IReadOnlyList<ServerScoreEntry> GetGlobalScores(bool isDescending, int scoreLimit = DefaultTopPlayersLimit)
        {
            int effectiveScoreLimit = DefaultTopPlayersLimit;
            if (scoreLimit > 0)
            {
                effectiveScoreLimit = scoreLimit;
            }

            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    IQueryable<ServerScoreEntry> scoreQuery = databaseContext.Users.Select(userEntity => new ServerScoreEntry
                    {
                        Username = userEntity.Username,
                        TotalScore = userEntity.MatchResults
                            .Where(matchResult => matchResult.FinalScore != null)
                            .Sum(matchResult => matchResult.FinalScore!.Value)
                    });

                    if (isDescending)
                    {
                        scoreQuery = scoreQuery.OrderByDescending(scoreEntry => scoreEntry.TotalScore);
                    }
                    else
                    {
                        scoreQuery = scoreQuery.OrderBy(scoreEntry => scoreEntry.TotalScore);
                    }

                    List<ServerScoreEntry> topScoreEntries = scoreQuery.Take(effectiveScoreLimit).ToList();

                    for (int entryIndex = 0; entryIndex < topScoreEntries.Count; entryIndex++)
                    {
                        topScoreEntries[entryIndex] = topScoreEntries[entryIndex] with { Rank = entryIndex + 1 };
                    }

                    return topScoreEntries;
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while loading leaderboard on server.");
                throw new DataOperationException("Could not retrieve global scores from the database.", databaseException);
            }
        }
    }
}
