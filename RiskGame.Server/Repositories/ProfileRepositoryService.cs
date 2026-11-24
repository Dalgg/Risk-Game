using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Contracts.Profile;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public sealed class ProfileRepositoryService : IProfileRepository
    {
        private const int WinningPosition = 1;
        private const int RecentMatchesLimit = 5;
        private readonly Func<RiskGameContext> createContext;

        public ProfileRepositoryService() : this(CreateDatabaseContext)
        {
        }

        public ProfileRepositoryService(Func<RiskGameContext> createContext)
        {
            this.createContext = createContext;
        }

        public UserProfile GetProfile(int userId)
        {
            try
            {
                using RiskGameContext databaseContext = createContext();
                User user = FindUser(databaseContext.Users.AsNoTracking(), userId);
                return BuildProfile(databaseContext, user);
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not retrieve the player profile.", databaseException);
            }
        }

        public UserProfile UpdateNickname(int userId, string nickname)
        {
            string normalizedNickname = ProfileCustomizationRules.NormalizeNickname(nickname);
            try
            {
                using RiskGameContext databaseContext = createContext();
                User user = FindUser(databaseContext.Users, userId);
                user.Nickname = normalizedNickname;
                databaseContext.SaveChanges();
                return BuildProfile(databaseContext, user);
            }
            catch (DbUpdateException databaseUpdateException)
            {
                throw new DataOperationException("Could not save the nickname.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not connect to save the nickname.", databaseException);
            }
        }

        public UserProfile UpdateAvatar(int userId, string avatarReference)
        {
            string selectedAvatar = ProfileCustomizationRules.ValidateAvatar(avatarReference);
            try
            {
                using RiskGameContext databaseContext = createContext();
                User user = FindUser(databaseContext.Users, userId);
                user.AvatarReference = selectedAvatar;
                databaseContext.SaveChanges();
                return BuildProfile(databaseContext, user);
            }
            catch (DbUpdateException databaseUpdateException)
            {
                throw new DataOperationException("Could not save the profile avatar.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not connect to save the avatar.", databaseException);
            }
        }

        private static User FindUser(IQueryable<User> users, int userId)
        {
            User? user = users.FirstOrDefault(candidate => candidate.IdUser == userId);
            if (user == null)
            {
                throw new ProfileNotFoundException("The account no longer exists.");
            }
            return user;
        }

        private static UserProfile BuildProfile(RiskGameContext databaseContext, User user)
        {
            string avatarReference = string.Empty;
            if (user.AvatarReference != null)
            {
                avatarReference = user.AvatarReference;
            }
            IQueryable<MatchResult> results = databaseContext.MatchResults.AsNoTracking()
                .Where(result => result.IdUser == user.IdUser);
            UserProfile profile = new UserProfile
            {
                UserId = user.IdUser, Username = user.Username, Nickname = user.Nickname,
                AvatarReference = avatarReference, MatchesPlayed = results.Count(),
                MatchesWon = results.Count(result => result.FinalPosition == WinningPosition)
            };
            profile.RecentMatches.AddRange(GetRecentMatches(databaseContext, user.IdUser));
            return profile;
        }

        private static List<RecentMatchResult> GetRecentMatches(RiskGameContext databaseContext, int userId)
        {
            List<MatchResult> results = databaseContext.MatchResults.AsNoTracking()
                .Include(result => result.IdMatchNavigation)
                .Where(result => result.IdUser == userId && result.IdMatchNavigation.EndDate.HasValue)
                .OrderByDescending(result => result.IdMatchNavigation.EndDate)
                .ThenByDescending(result => result.IdMatch)
                .ThenByDescending(result => result.IdMatchResult)
                .Take(RecentMatchesLimit)
                .ToList();
            List<RecentMatchResult> history = new List<RecentMatchResult>();
            foreach (MatchResult result in results)
            {
                history.Add(BuildRecentMatch(result));
            }
            return history;
        }

        private static RecentMatchResult BuildRecentMatch(MatchResult result)
        {
            DateTime endDate = DateTime.SpecifyKind(result.IdMatchNavigation.EndDate.GetValueOrDefault(),
                DateTimeKind.Utc);
            string endReason = string.Empty;
            if (result.IdMatchNavigation.EndReason != null)
            {
                endReason = result.IdMatchNavigation.EndReason;
            }
            return new RecentMatchResult
            {
                MatchId = result.IdMatch,
                EndTimeUnixSeconds = new DateTimeOffset(endDate).ToUnixTimeSeconds(),
                IsWinner = result.FinalPosition.GetValueOrDefault() == WinningPosition,
                ClaimedColumns = result.FinalScore.GetValueOrDefault(),
                HasClaimedColumns = result.FinalScore.HasValue,
                EndReason = endReason,
                HasResult = result.FinalPosition.HasValue
            };
        }

        private static RiskGameContext CreateDatabaseContext()
        {
            return new RiskGameContext();
        }
    }
}
