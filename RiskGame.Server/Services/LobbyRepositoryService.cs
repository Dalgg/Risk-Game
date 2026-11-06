using System;
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
                        CreationDate = DateTime.Now,
                        ClosureDate = null
                    };

                    databaseContext.Lobbies.Add(newLobbyEntity);
                    databaseContext.SaveChanges();
                    return newLobbyEntity;
                }
            }
            catch (DbUpdateException dbUpdateException)
            {
                JsonLogger.Error(dbUpdateException, "Persistence error (DbUpdateException) while creating lobby.");
                throw new DataOperationException("Could not persist the new lobby in the database.", dbUpdateException);
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
                        StartDate = DateTime.Now,
                        EndDate = null,
                        EndReason = null
                    };

                    databaseContext.Matches.Add(newMatchEntity);
                    databaseContext.SaveChanges();
                    return newMatchEntity.IdMatch;
                }
            }
            catch (DbUpdateException dbUpdateException)
            {
                JsonLogger.Error(dbUpdateException, "Persistence error (DbUpdateException) while creating match for lobby {0}.", lobbyId);
                throw new DataOperationException("Could not persist the match in the database.", dbUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while creating match for lobby {0}.", lobbyId);
                throw new DataOperationException("Could not connect to the database to create the match.", databaseException);
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
