using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Profile;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class ProfileRepositoryService : IProfileRepository
    {
        private const int WinningPosition = 1;

        /// <summary>Reads a profile and counts persisted results without tracking entities.</summary>
        /// <param name="userId">The authenticated registered user identifier.</param>
        /// <returns>The matching registered player's profile.</returns>
        /// <exception cref="ProfileNotFoundException">The account no longer exists.</exception>
        /// <exception cref="DataOperationException">The database could not be queried.</exception>
        public UserProfile GetProfile(int userId)
        {
            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    User? registeredUser = databaseContext.Users
                        .AsNoTracking()
                        .Where(user => user.IdUser == userId)
                        .FirstOrDefault();
                    if (registeredUser == null)
                    {
                        throw new ProfileNotFoundException("The account no longer exists.");
                    }

                    string avatarReference = string.Empty;
                    if (registeredUser.AvatarReference != null)
                    {
                        avatarReference = registeredUser.AvatarReference;
                    }

                    IQueryable<MatchResult> playerResults = databaseContext.MatchResults
                        .AsNoTracking()
                        .Where(matchResult => matchResult.IdUser == userId);

                    return new UserProfile
                    {
                        UserId = registeredUser.IdUser,
                        Username = registeredUser.Username,
                        AvatarReference = avatarReference,
                        MatchesPlayed = playerResults.Count(),
                        MatchesWon = playerResults.Count(matchResult => matchResult.FinalPosition == WinningPosition)
                    };
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Failed to retrieve profile for user {0}.", userId);
                throw new DataOperationException("Could not retrieve the player profile.", databaseException);
            }
        }
    }
}
