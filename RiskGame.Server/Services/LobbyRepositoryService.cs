using System;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public class LobbyRepositoryService
    {
        public Lobby GetActiveLobbyByCode(string lobbyCode)
        {
            string normalizedCode = (lobbyCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                throw new LobbyNotFoundException("The requested lobby code does not exist or is closed.");
            }

            try
            {
                using (RiskGameContext context = new RiskGameContext())
                {
                    Lobby? activeLobby = context.Lobbies
                        .AsNoTracking()
                        .FirstOrDefault(lobby => lobby.LobbyCode == normalizedCode && lobby.ClosureDate == null);

                    if (activeLobby == null)
                    {
                        throw new LobbyNotFoundException("The requested lobby code does not exist or is closed.");
                    }

                    return activeLobby;
                }
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) while querying lobby with code {0}.", normalizedCode);
                throw new DataOperationException("Could not query the lobby from the database.", ex);
            }
        }
    }
}
