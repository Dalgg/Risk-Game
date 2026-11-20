using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public sealed class ScoreboardRepositoryService : IScoreboardRepository
    {
        private const int WinningPosition = 1;
        private const string AcceptedFriendship = "accepted";
        private readonly Func<RiskGameContext> createContext;

        public ScoreboardRepositoryService(Func<RiskGameContext> createContext)
        {
            this.createContext = createContext;
        }

        public IReadOnlyList<ServerScoreEntry> GetScores(ScoreboardFilter filter)
        {
            try
            {
                using (RiskGameContext databaseContext = createContext())
                {
                    IQueryable<User> users = FilterUsers(databaseContext, filter);
                    IQueryable<ServerScoreEntry> scores = users.Select(user => new ServerScoreEntry
                    {
                        UserId = user.IdUser,
                        Username = user.Username,
                        TotalWins = user.MatchResults.Count(result => result.FinalPosition == WinningPosition),
                        TotalScore = user.MatchResults.Sum(result => result.FinalScore.GetValueOrDefault())
                    });
                    return LoadOrderedScores(scores, filter);
                }
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not retrieve the scoreboard.", databaseException);
            }
        }

        private IQueryable<User> FilterUsers(RiskGameContext databaseContext, ScoreboardFilter filter)
        {
            IQueryable<User> users = databaseContext.Users.AsNoTracking();
            if (filter.FriendsOnly)
            {
                IQueryable<Friendship> friendships = databaseContext.Friendships.AsNoTracking()
                    .Where(friendship => friendship.Status == AcceptedFriendship);
                IQueryable<int> recipients = friendships
                    .Where(friendship => friendship.IdUser1 == filter.UserId)
                    .Select(friendship => friendship.IdUser2);
                IQueryable<int> senders = friendships
                    .Where(friendship => friendship.IdUser2 == filter.UserId)
                    .Select(friendship => friendship.IdUser1);
                users = users.Where(user => user.IdUser == filter.UserId
                    || recipients.Contains(user.IdUser) || senders.Contains(user.IdUser));
            }
            return users;
        }

        private IReadOnlyList<ServerScoreEntry> LoadOrderedScores(IQueryable<ServerScoreEntry> scores,
            ScoreboardFilter filter)
        {
            IOrderedQueryable<ServerScoreEntry> ordered = scores.OrderByDescending(score => score.TotalWins)
                .ThenByDescending(score => score.TotalScore);
            if (!filter.Descending)
            {
                ordered = scores.OrderBy(score => score.TotalWins).ThenBy(score => score.TotalScore);
            }
            List<ServerScoreEntry> entries = ordered.ThenBy(score => score.Username)
                .ThenBy(score => score.UserId)
                .Take(filter.TopLimit)
                .ToList();
            return entries.AsReadOnly();
        }
    }
}
