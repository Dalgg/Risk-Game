using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public sealed class MatchResultsRepositoryService : ICompletedMatchRepository
    {
        private const string VictoryEndReason = "Victory";
        private readonly Func<RiskGameContext> createContext;
        private readonly MatchResultBuilder resultBuilder = new MatchResultBuilder();

        public MatchResultsRepositoryService(Func<RiskGameContext> createContext)
        {
            this.createContext = createContext;
        }

        public void SaveCompletedMatch(ActiveMatch match)
        {
            if (!match.IsFinished)
            {
                throw new MatchActionException("Only a completed match can be persisted.");
            }
            try
            {
                using (RiskGameContext databaseContext = createContext())
                {
                    using (IDbContextTransaction transaction = databaseContext.Database
                        .BeginTransaction(IsolationLevel.Serializable))
                    {
                        SaveResults(databaseContext, match);
                        transaction.Commit();
                    }
                }
            }
            catch (DbUpdateException databaseUpdateException)
            {
                throw new DataOperationException("Could not save the completed match.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not access the match results database.", databaseException);
            }
        }

        private void SaveResults(RiskGameContext databaseContext, ActiveMatch match)
        {
            RiskGame.Models.Match? storedMatch = databaseContext.Matches
                .Include(candidate => candidate.MatchState)
                .FirstOrDefault(candidate => candidate.IdMatch == match.MatchId);
            if (storedMatch == null)
            {
                throw new MatchNotFoundException("The persisted match does not exist.");
            }
            if (!storedMatch.EndDate.HasValue)
            {
                storedMatch.EndDate = DateTime.UtcNow;
                storedMatch.EndReason = VictoryEndReason;
                databaseContext.MatchResults.AddRange(resultBuilder.Build(match));
                if (storedMatch.MatchState != null)
                {
                    databaseContext.MatchStates.Remove(storedMatch.MatchState);
                }
                databaseContext.SaveChanges();
            }
        }
    }
}
