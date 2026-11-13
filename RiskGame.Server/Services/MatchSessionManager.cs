using System;
using System.Collections.Generic;
using RiskGame.Contracts.Lobby;

namespace RiskGame.Server.Services
{
    public sealed class MatchSessionManager
    {
        private const int MinimumValidIdentifier = 1;
        private const int MinimumParticipants = 2;
        private const int MaximumParticipants = 4;
        private readonly object matchLock = new object();
        private readonly Dictionary<int, ActiveMatch> matchesById = new Dictionary<int, ActiveMatch>();
        
        public ActiveMatch CreateMatch(int matchId, LobbyDetails lobbyDetails)
        {
            ValidateParticipants(lobbyDetails);
            if (matchId < MinimumValidIdentifier)
            {
                throw new InvalidOperationException("A persisted match identifier is required.");
            }
            lock (matchLock)
            {
                if (matchesById.ContainsKey(matchId))
                {
                    throw new InvalidOperationException("The match has already been initialized.");
                }
                ActiveMatch activeMatch = new ActiveMatch(matchId, lobbyDetails);
                matchesById.Add(matchId, activeMatch);
                return activeMatch;
            }
        }

        private void ValidateParticipants(LobbyDetails lobbyDetails)
        {
            if (lobbyDetails.Players.Count < MinimumParticipants || lobbyDetails.Players.Count > MaximumParticipants)
            {
                throw new InvalidOperationException("A match requires two to four participants.");
            }
            HashSet<int> participantIdentifiers = new HashSet<int>();
            foreach (LobbyPlayer lobbyPlayer in lobbyDetails.Players)
            {
                if (lobbyPlayer.Player == null || !participantIdentifiers.Add(lobbyPlayer.Player.UserId))
                {
                    throw new InvalidOperationException("Each participant must have a distinct player identifier.");
                }
            }
        }
    }
}
