using System;
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
            return new UserProfile
            {
                UserId = user.IdUser, Username = user.Username, Nickname = user.Nickname,
                AvatarReference = avatarReference, MatchesPlayed = results.Count(),
                MatchesWon = results.Count(result => result.FinalPosition == WinningPosition)
            };
        }

        private static RiskGameContext CreateDatabaseContext()
        {
            return new RiskGameContext();
        }
    }
}
