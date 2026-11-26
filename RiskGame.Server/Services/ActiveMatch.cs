using System;
using System.Collections.Generic;
using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
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
        private const int DefaultTurnDurationSeconds = 60;
        private const int MinimumTurnDurationSeconds = 1;
        private const int NoParticipants = 0;
        private const int LastParticipant = 1;
        private const int NoWinnerUserId = 0;
        private readonly TimeProvider timeProvider;
        private long turnStartedTimestamp;
        private int currentParticipantIndex = FirstParticipantIndex;
        private bool hasRolledThisTurn;

        public ActiveMatch(int matchId, LobbyDetails lobbyDetails) : this(matchId, lobbyDetails, TimeProvider.System)
        {
        }

        public ActiveMatch(int matchId, LobbyDetails lobbyDetails, TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
            MatchId = matchId;
            LobbyId = lobbyDetails.LobbyId;
            RulesVariant = lobbyDetails.Rules.RulesVariant;
            ColumnsToWin = StandardColumnsToWin;
            if (RulesVariant == RulesVariant.Fast)
            {
                ColumnsToWin = FastColumnsToWin;
            }
            TurnDurationSeconds = lobbyDetails.Rules.TurnDurationSeconds;
            if (TurnDurationSeconds < MinimumTurnDurationSeconds)
            {
                TurnDurationSeconds = DefaultTurnDurationSeconds;
            }
            List<MatchParticipant> participants = new List<MatchParticipant>();
            foreach (LobbyPlayer player in lobbyDetails.Players)
            {
                participants.Add(new MatchParticipant(player, timeProvider));
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
            turnStartedTimestamp = timeProvider.GetTimestamp();
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

        public bool LastTurnTimedOut { get; private set; }

        public bool IsFinished { get; private set; }

        public int WinnerUserId { get; private set; }

        public MatchEndReason EndReason { get; private set; }

        public int ColumnsToWin { get; }

        public bool CanStop
        {
            get
            {
                bool isTurnReady = !IsFinished && !AwaitingPairChoice;
                bool hasProgress = SelectedPairIndex != NoSelectedPair
                    && TurnProgress.StepsByColumn.Count > NoSelectedPair;
                return isTurnReady && hasProgress;
            }
        }

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
            hasRolledThisTurn = true;
            GetParticipant(CurrentTurnUserId).RecordTurnActivity();
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
                FinishMatch(MatchEndReason.Victory, CurrentTurnUserId);
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
            if (GetRemainingTurnTime() == TimeSpan.Zero)
            {
                throw new MatchActionException("The turn time has expired.");
            }
        }

        public TimeSpan GetRemainingTurnTime()
        {
            TimeSpan remaining = TimeSpan.Zero;
            if (!IsFinished)
            {
                TimeSpan elapsed = timeProvider.GetElapsedTime(turnStartedTimestamp);
                remaining = TimeSpan.FromSeconds(TurnDurationSeconds) - elapsed;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }
            }
            return remaining;
        }

        public bool ExpireTurnIfNeeded()
        {
            bool expired = !IsFinished && GetRemainingTurnTime() == TimeSpan.Zero;
            if (expired)
            {
                MatchParticipant participant = GetParticipant(CurrentTurnUserId);
                bool shouldCountMissedTurn = participant.IsConnected && !hasRolledThisTurn;
                if (shouldCountMissedTurn && participant.RecordMissedTurn())
                {
                    AbandonParticipant(participant.UserId);
                }
                else
                {
                    AdvanceTurn(true);
                }
                LastTurnTimedOut = !IsFinished;
            }
            return expired;
        }

        private void FinishMatch(MatchEndReason reason, int winnerUserId)
        {
            WinnerUserId = winnerUserId;
            EndReason = reason;
            IsFinished = true;
            LastTurnUserId = CurrentTurnUserId;
            LastTurnBusted = false;
            LastTurnTimedOut = false;
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
            LastTurnTimedOut = false;
            int checkedParticipants = NoParticipants;
            do
            {
                currentParticipantIndex = (currentParticipantIndex + NextParticipantStep) % Participants.Count;
                checkedParticipants++;
            }
            while (Participants[currentParticipantIndex].HasAbandoned && checkedParticipants < Participants.Count);
            CurrentTurnUserId = Participants[currentParticipantIndex].UserId;
            hasRolledThisTurn = false;
            turnStartedTimestamp = timeProvider.GetTimestamp();
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
            MatchParticipant participant = GetParticipant(userId);
            participant.SetConnected(isConnected);
            Revision++;
            if (!IsFinished)
            {
                ResolveParticipantChanges();
            }
        }

        public MatchParticipant GetParticipant(int userId)
        {
            MatchParticipant? participant = Participants.FirstOrDefault(player => player.UserId == userId);
            if (participant == null)
            {
                throw new InvalidSessionException("The player is not part of this match.");
            }
            return participant;
        }

        public void ProcessConnectionTimeouts()
        {
            if (!IsFinished)
            {
                foreach (MatchParticipant participant in Participants)
                {
                    if (participant.RefreshConnection())
                    {
                        Revision++;
                    }
                }
                ResolveParticipantChanges();
            }
        }

        public bool AbandonParticipant(int userId)
        {
            MatchParticipant participant = GetParticipant(userId);
            bool changed = !IsFinished && !participant.HasAbandoned;
            if (changed)
            {
                participant.Abandon();
                Revision++;
                ResolveParticipantChanges();
            }
            return changed;
        }

        private void ResolveParticipantChanges()
        {
            List<MatchParticipant> remaining = Participants.Where(player => !player.HasAbandoned).ToList();
            if (remaining.Count == NoParticipants)
            {
                FinishMatch(MatchEndReason.Cancelled, NoWinnerUserId);
            }
            else if (remaining.Count == LastParticipant && remaining[FirstParticipantIndex].IsConnected)
            {
                FinishMatch(MatchEndReason.Abandonment, remaining[FirstParticipantIndex].UserId);
            }
            else if (GetParticipant(CurrentTurnUserId).HasAbandoned)
            {
                AdvanceTurn(true);
            }
        }
    }
}
