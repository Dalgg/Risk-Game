using System;
using System.Collections.Generic;
using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;

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
        private const long InitialRevision = 1;
        private const int NoSelectedPair = 0;

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

        public long Revision { get; private set; } = InitialRevision;

        public long RollId { get; private set; }

        public IReadOnlyList<int> Dice { get; private set; } = Array.Empty<int>();

        public IReadOnlyList<DicePairCombination> PairOptions { get; private set; } =
            Array.Empty<DicePairCombination>();

        public bool AwaitingPairChoice { get; private set; }

        public int SelectedPairIndex { get; private set; }

        public void ApplyRoll(IReadOnlyList<int> dice, long expectedRollId)
        {
            if (AwaitingPairChoice || expectedRollId != RollId)
            {
                throw new MatchActionException("The previous roll is pending or the request is outdated.");
            }
            IReadOnlyList<DicePairCombination> combinations = DicePairGenerator.BuildCombinations(dice);
            Dice = new List<int>(dice).AsReadOnly();
            PairOptions = combinations;
            RollId++;
            SelectedPairIndex = NoSelectedPair;
            AwaitingPairChoice = true;
            Revision++;
        }

        public void ChoosePair(long rollId, int pairIndex)
        {
            if (!AwaitingPairChoice || rollId != RollId)
            {
                throw new MatchActionException("This roll no longer accepts a pair choice.");
            }
            if (!PairOptions.Any(combination => combination.PairIndex == pairIndex))
            {
                throw new MatchActionException("The selected pair is not part of the current roll.");
            }
            SelectedPairIndex = pairIndex;
            AwaitingPairChoice = false;
            Revision++;
        }

        public void UpdateConnection(int userId, bool isConnected)
        {
            foreach (MatchParticipant participant in Participants)
            {
                if (participant.UserId == userId)
                {
                    participant.SetConnected(isConnected);
                    Revision++;
                    break;
                }
            }
        }
    }
}
