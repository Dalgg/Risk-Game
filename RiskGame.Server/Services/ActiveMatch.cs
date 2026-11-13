using System;
using System.Collections.Generic;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;

namespace RiskGame.Server.Services
{
    public sealed class ActiveMatch
    {
        private const int FirstParticipantIndex = 0;
        private const int MinimumColumnNumber = 2;
        private const int MaximumColumnNumber = 12;
        private const int CentralColumnNumber = 7;
        private const int CentralColumnHeight = 13;
        private const int ColumnHeightDifference = 2;

        public ActiveMatch(int matchId, LobbyDetails lobbyDetails)
        {
            MatchId = matchId;
            LobbyId = lobbyDetails.LobbyId;
            RulesVariant = lobbyDetails.Rules.RulesVariant;
            TurnDurationSeconds = lobbyDetails.Rules.TurnDurationSeconds;
            List<MatchParticipant> participants = new List<MatchParticipant>();
            foreach (LobbyPlayer player in lobbyDetails.Players)
            {
                participants.Add(new MatchParticipant(player));
            }
            Participants = participants.AsReadOnly();
            CurrentTurnUserId = participants[FirstParticipantIndex].UserId;

            List<MatchColumn> columns = new List<MatchColumn>();
            for (int columnNumber = MinimumColumnNumber; columnNumber <= MaximumColumnNumber; columnNumber++)
            {
                int maximumSteps = CentralColumnHeight - ColumnHeightDifference 
                * Math.Abs(CentralColumnNumber - columnNumber);
                columns.Add(new MatchColumn(columnNumber, maximumSteps, Participants));
            }
            Columns = columns.AsReadOnly();
        }

        public int MatchId { get; }

        public int LobbyId { get; }

        public RulesVariant RulesVariant { get; }

        public int TurnDurationSeconds { get; }

        public int CurrentTurnUserId { get; }

        public IReadOnlyList<MatchParticipant> Participants { get; }

        public IReadOnlyList<MatchColumn> Columns { get; }
    }
}
