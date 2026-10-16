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

        public IReadOnlyList<ServerScoreEntry> GetGlobalScores(bool isDescending, int limit = DefaultTopPlayersLimit)
        {
            int effectiveLimit = limit > 0 ? limit : DefaultTopPlayersLimit;

            try
            {
                using (RiskGameContext context = new RiskGameContext())
                {
                    IQueryable<ServerScoreEntry> query = context.Users.Select(user => new ServerScoreEntry
                    {
                        Username = user.Username,
                        TotalScore = user.MatchResults.Sum(matchResult => matchResult.FinalScore ?? 0)
                    });

                    query = isDescending
                        ? query.OrderByDescending(score => score.TotalScore)
                        : query.OrderBy(score => score.TotalScore);

                    List<ServerScoreEntry> topScores = query.Take(effectiveLimit).ToList();

                    for (int index = 0; index < topScores.Count; index++)
                    {
                        topScores[index] = topScores[index] with { Rank = index + 1 };
                    }

                    return topScores;
                }
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) while loading leaderboard on server.");
                throw new DataOperationException("Could not retrieve global scores from the database.", ex);
            }
        }
    }
}
