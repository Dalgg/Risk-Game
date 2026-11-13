using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public class LobbyRepositoryService
    {
        private const string AllowedCodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int LobbyCodeLength = 6;
        private const int MaxCodeGenerationAttempts = 10;
        private const int MinimumValidIdentifier = 1;
        private const int MinutesForExpiration = 30;

        public Lobby GetActiveLobbyByCode(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                throw new LobbyNotFoundException("The requested lobby code does not exist or is closed.");
            }

            string normalizedLobbyCode = lobbyCode.Trim();

            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    Lobby? activeLobby = databaseContext.Lobbies
                        .AsNoTracking()
                        .FirstOrDefault(lobbyEntity => lobbyEntity.LobbyCode == normalizedLobbyCode && lobbyEntity.ClosureDate == null);

                    if (activeLobby == null)
                    {
                        throw new LobbyNotFoundException("The requested lobby code does not exist or is closed.");
                    }

                    return activeLobby;
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while querying lobby with code {0}.", normalizedLobbyCode);
                throw new DataOperationException("Could not query the lobby from the database.", databaseException);
            }
        }

        public Lobby CreateLobbyInDatabase(string rulesVariantName, int turnDurationSeconds)
        {
            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    string generatedCode = GenerateUniqueLobbyCode(databaseContext);
                    Lobby newLobbyEntity = new Lobby
                    {
                        LobbyCode = generatedCode,
                        RulesVariant = rulesVariantName,
                        TurnDuration = turnDurationSeconds,
                        CreationDate = DateTime.UtcNow,
                        ClosureDate = null
                    };

                    databaseContext.Lobbies.Add(newLobbyEntity);
                    databaseContext.SaveChanges();
                    return newLobbyEntity;
                }
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Persistence error (DbUpdateException) while creating lobby.");
                throw new DataOperationException("Could not persist the new lobby in the database.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while creating lobby.");
                throw new DataOperationException("Could not connect to the database to create the lobby.", databaseException);
            }
        }

        public int CreateMatchInDatabase(int lobbyId)
        {
            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    Match newMatchEntity = new Match
                    {
                        IdLobby = lobbyId,
                        StartDate = DateTime.UtcNow,
                        EndDate = null,
                        EndReason = null
                    };

                    databaseContext.Matches.Add(newMatchEntity);
                    databaseContext.SaveChanges();
                    return newMatchEntity.IdMatch;
                }
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Persistence error (DbUpdateException) while creating match for lobby {0}.", lobbyId);
                throw new DataOperationException("Could not persist the match in the database.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while creating match for lobby {0}.", lobbyId);
                throw new DataOperationException("Could not connect to the database to create the match.", databaseException);
            }
        }

        public List<User> GetAcceptedFriends(int userId)
        {
            if (userId < MinimumValidIdentifier)
            {
                return new List<User>();
            }

            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    List<Friendship> friendshipList = databaseContext.Friendships
                        .AsNoTracking()
                        .Where(friendshipRecord =>
                            (friendshipRecord.IdUser1 == userId || friendshipRecord.IdUser2 == userId)
                            && friendshipRecord.Status == "accepted")
                        .ToList();

                    List<int> friendUserIds = new List<int>();
                    foreach (Friendship friendshipRecord in friendshipList)
                    {
                        int friendId;
                        if (friendshipRecord.IdUser1 == userId)
                        {
                            friendId = friendshipRecord.IdUser2;
                        }
                        else
                        {
                            friendId = friendshipRecord.IdUser1;
                        }

                        if (!friendUserIds.Contains(friendId))
                        {
                            friendUserIds.Add(friendId);
                        }
                    }

                    if (friendUserIds.Count == 0)
                    {
                        return new List<User>();
                    }

                    List<User> friendUsers = databaseContext.Users
                        .AsNoTracking()
                        .Where(userEntity => friendUserIds.Contains(userEntity.IdUser))
                        .ToList();

                    return friendUsers;
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while loading friends for user {0}.", userId);
                throw new DataOperationException("Could not query friends list from the database.", databaseException);
            }
        }

        public void CreateLobbyInvitation(int lobbyId, int senderUserId, int recipientUserId)
        {
            if (lobbyId < MinimumValidIdentifier || senderUserId < MinimumValidIdentifier || recipientUserId < MinimumValidIdentifier)
            {
                throw new ArgumentException("The invitation parameters must be positive identifiers.");
            }

            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    string uniqueInvitationCode = GenerateUniqueLobbyCode(databaseContext);
                    DateTime currentDateTime = DateTime.UtcNow;
                    DateTime expirationDateTime = currentDateTime.AddMinutes(MinutesForExpiration);

                    LobbyInvitation newInvitationEntity = new LobbyInvitation
                    {
                        IdLobby = lobbyId,
                        IdSender = senderUserId,
                        IdRecipient = recipientUserId,
                        InvitationCode = uniqueInvitationCode,
                        Status = "pending",
                        SentDate = currentDateTime,
                        ExpirationDate = expirationDateTime
                    };

                    databaseContext.LobbyInvitations.Add(newInvitationEntity);
                    databaseContext.SaveChanges();
                }
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Persistence error (DbUpdateException) while creating lobby invitation.");
                throw new DataOperationException("Could not persist the lobby invitation in the database.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while creating lobby invitation.");
                throw new DataOperationException("Could not connect to the database to create the lobby invitation.", databaseException);
            }
        }

        private static string GenerateUniqueLobbyCode(RiskGameContext databaseContext)
        {
            for (int attemptIndex = 0; attemptIndex < MaxCodeGenerationAttempts; attemptIndex++)
            {
                char[] codeCharacters = new char[LobbyCodeLength];
                for (int charIndex = 0; charIndex < LobbyCodeLength; charIndex++)
                {
                    int randomIndex = RandomNumberGenerator.GetInt32(0, AllowedCodeCharacters.Length);
                    codeCharacters[charIndex] = AllowedCodeCharacters[randomIndex];
                }

                string candidateCode = new string(codeCharacters);
                bool codeExists = databaseContext.Lobbies.Any(
                    lobbyEntity => lobbyEntity.LobbyCode == candidateCode && lobbyEntity.ClosureDate == null);

                if (!codeExists)
                {
                    return candidateCode;
                }
            }

            throw new DataOperationException("Could not generate a unique lobby code.");
        }
    }
}
