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
        private const int NoPreferredColumn = 0;
        private const int NextParticipantStep = 1;
        private const int StandardColumnsToWin = 3;
        private const int FastColumnsToWin = 2;
        private int currentParticipantIndex = FirstParticipantIndex;

        public ActiveMatch(int matchId, LobbyDetails lobbyDetails)
        {
            MatchId = matchId;
            LobbyId = lobbyDetails.LobbyId;
            RulesVariant = lobbyDetails.Rules.RulesVariant;
            ColumnsToWin = StandardColumnsToWin;
            if (RulesVariant == RulesVariant.Fast)
            {
                ColumnsToWin = FastColumnsToWin;
            }
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
            TurnProgress = new MatchTurnProgress(CurrentTurnUserId, Columns);
        }

        public int MatchId { get; }

        public int LobbyId { get; }

        public RulesVariant RulesVariant { get; }

        public int TurnDurationSeconds { get; }

        public int CurrentTurnUserId { get; private set; }

        public IReadOnlyList<MatchParticipant> Participants { get; }

        public IReadOnlyList<MatchColumn> Columns { get; }

        public MatchTurnProgress TurnProgress { get; private set; }

        public long TurnId { get; private set; } = InitialRevision;

        public int LastTurnUserId { get; private set; }

        public bool LastTurnBusted { get; private set; }

        public bool IsFinished { get; private set; }

        public int WinnerUserId { get; private set; }

        public int ColumnsToWin { get; }

        public bool CanStop => !IsFinished && !AwaitingPairChoice && SelectedPairIndex != NoSelectedPair
            && TurnProgress.StepsByColumn.Count > NoSelectedPair;

        public long Revision { get; private set; } = InitialRevision;

        public long RollId { get; private set; }

        public IReadOnlyList<int> Dice { get; private set; } = Array.Empty<int>();

        public IReadOnlyList<DicePairCombination> PairOptions { get; private set; } =
            Array.Empty<DicePairCombination>();

        public bool AwaitingPairChoice { get; private set; }

        public int SelectedPairIndex { get; private set; }

        public void ApplyRoll(IReadOnlyList<int> dice, long expectedRollId)
        {
            EnsureActive();
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
            if (!PairOptions.Any(combination => TurnProgress.IsPlayable(combination)))
            {
                AdvanceTurn(true);
            }
        }

        public void StopTurn(long turnId)
        {
            EnsureActive();
            if (turnId != TurnId || !CanStop)
            {
                throw new MatchActionException("The turn cannot be stopped before choosing a valid advance.");
            }
            foreach (MatchColumn column in Columns)
            {
                if (TurnProgress.StepsByColumn.ContainsKey(column.ColumnNumber))
                {
                    column.SaveProgress(CurrentTurnUserId, TurnProgress.StepsByColumn[column.ColumnNumber]);
                }
            }
            int claimedColumns = Columns.Count(column => column.IsClaimed && column.OwnerUserId == CurrentTurnUserId);
            if (claimedColumns >= ColumnsToWin)
            {
                FinishMatch();
            }
            else
            {
                AdvanceTurn(false);
            }
        }

        private void EnsureActive()
        {
            if (IsFinished)
            {
                throw new MatchActionException("The match has already finished.");
            }
        }

        private void FinishMatch()
        {
            WinnerUserId = CurrentTurnUserId;
            IsFinished = true;
            LastTurnUserId = CurrentTurnUserId;
            LastTurnBusted = false;
            TurnProgress = new MatchTurnProgress(CurrentTurnUserId, Columns);
            Dice = Array.Empty<int>();
            PairOptions = Array.Empty<DicePairCombination>();
            AwaitingPairChoice = false;
            SelectedPairIndex = NoSelectedPair;
            TurnId++;
            Revision++;
        }

        private void AdvanceTurn(bool busted)
        {
            LastTurnUserId = CurrentTurnUserId;
            LastTurnBusted = busted;
            currentParticipantIndex = (currentParticipantIndex + NextParticipantStep) % Participants.Count;
            CurrentTurnUserId = Participants[currentParticipantIndex].UserId;
            TurnProgress = new MatchTurnProgress(CurrentTurnUserId, Columns);
            TurnId++;
            Dice = Array.Empty<int>();
            PairOptions = Array.Empty<DicePairCombination>();
            SelectedPairIndex = NoSelectedPair;
            AwaitingPairChoice = false;
            Revision++;
        }

        public void ChoosePair(long rollId, int pairIndex, int preferredColumn = NoPreferredColumn)
        {
            EnsureActive();
            if (!AwaitingPairChoice || rollId != RollId)
            {
                throw new MatchActionException("This roll no longer accepts a pair choice.");
            }
            DicePairCombination? selectedCombination = PairOptions
                .FirstOrDefault(combination => combination.PairIndex == pairIndex);
            if (selectedCombination == null)
            {
                throw new MatchActionException("The selected pair is not part of the current roll.");
            }
            TurnProgress.ApplyCombination(selectedCombination, preferredColumn);
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
