using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class MatchSessionManager
    {
        private const int MinimumValidIdentifier = 1;
        private const int MinimumParticipants = 2;
        private const int MaximumParticipants = 4;
        private const int InvalidPlayerIdentifier = 0;
        private const int CompletedTurnOffset = 1;
        private const int EmptyQueue = 0;
        private const int CompletionRetrySeconds = 2;
        private const int StreamCheckSeconds = 1;
        private readonly object matchLock = new object();
        private readonly Dictionary<int, ActiveMatch> matchesById = new Dictionary<int, ActiveMatch>();
        private readonly Dictionary<int, Dictionary<int, IServerStreamWriter<BoardState>>> subscribersByMatchId =
            new Dictionary<int, Dictionary<int, IServerStreamWriter<BoardState>>>();
        private readonly ProfileSessionRegistry sessionRegistry;
        private readonly TimeProvider timeProvider;
        private readonly ICompletedMatchRepository completedMatchRepository;
        private readonly HashSet<int> storedCompletions = new HashSet<int>();
        private readonly Queue<AbandonedMatchPlayer> pendingAbandonments = new Queue<AbandonedMatchPlayer>();
        private readonly HashSet<AbandonedMatchPlayer> queuedAbandonments = new HashSet<AbandonedMatchPlayer>();
        private readonly Dictionary<int, long> failedCompletionAttempts = new Dictionary<int, long>();

        public MatchSessionManager(ProfileSessionRegistry sessionRegistry,
            ICompletedMatchRepository completedMatchRepository)
            : this(sessionRegistry, completedMatchRepository, TimeProvider.System)
        {
        }

        public MatchSessionManager(ProfileSessionRegistry sessionRegistry,
            ICompletedMatchRepository completedMatchRepository, TimeProvider timeProvider)
        {
            this.sessionRegistry = sessionRegistry;
            this.completedMatchRepository = completedMatchRepository;
            this.timeProvider = timeProvider;
        }
        
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
                ActiveMatch activeMatch = new ActiveMatch(matchId, lobbyDetails, timeProvider);
                matchesById.Add(matchId, activeMatch);
                subscribersByMatchId.Add(matchId, new Dictionary<int, IServerStreamWriter<BoardState>>());
                return activeMatch;
            }
        }

        public Task SubscribeMatchEvents(MatchParticipantSession participant,
            IServerStreamWriter<BoardState> responseStream, CancellationToken cancellationToken)
        {
            try
            {
                RegisterSubscriber(participant, responseStream);
                BroadcastBoardState(participant.MatchId);
                bool subscribed = true;
                TimeSpan checkInterval = TimeSpan.FromSeconds(StreamCheckSeconds);
                while (subscribed && !cancellationToken.WaitHandle.WaitOne(checkInterval))
                {
                    subscribed = IsSubscriptionActive(participant, responseStream);
                }
                return Task.CompletedTask;
            }
            finally
            {
                DisconnectSubscriber(participant.MatchId, participant.UserId, responseStream);
            }
        }

        private bool IsSubscriptionActive(MatchParticipantSession participant,
            IServerStreamWriter<BoardState> responseStream)
        {
            lock (matchLock)
            {
                Dictionary<int, IServerStreamWriter<BoardState>> subscribers =
                    subscribersByMatchId[participant.MatchId];
                bool registered = subscribers.ContainsKey(participant.UserId);
                bool activeParticipant = !matchesById[participant.MatchId]
                    .GetParticipant(participant.UserId).HasAbandoned;
                bool isSubscriptionActive = false;
                if (registered && activeParticipant)
                {
                    isSubscriptionActive =
                        ReferenceEquals(subscribers[participant.UserId], responseStream);
                }
                return isSubscriptionActive;
            }
        }

        private void RegisterSubscriber(MatchParticipantSession participant,
            IServerStreamWriter<BoardState> responseStream)
        {
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetAuthorizedMatch(participant);
                if (activeMatch.GetParticipant(participant.UserId).HasAbandoned)
                {
                    throw new MatchActionException("The participant has already abandoned this match.");
                }
                subscribersByMatchId[participant.MatchId][participant.UserId] = responseStream;
                activeMatch.UpdateConnection(participant.UserId, true);
                StoreCompletion(activeMatch);
            }
        }

        private bool DisconnectSubscriber(int matchId, int userId, IServerStreamWriter<BoardState> responseStream)
        {
            bool subscriptionRemoved = false;
            lock (matchLock)
            {
                if (subscribersByMatchId.ContainsKey(matchId))
                {
                    Dictionary<int, IServerStreamWriter<BoardState>> subscribers = subscribersByMatchId[matchId];
                    if (subscribers.ContainsKey(userId) && ReferenceEquals(subscribers[userId], responseStream))
                    {
                        subscribers.Remove(userId);
                        matchesById[matchId].UpdateConnection(userId, false);
                        subscriptionRemoved = true;
                    }
                }
            }
            if (subscriptionRemoved)
            {
                BroadcastBoardState(matchId);
            }
            return subscriptionRemoved;
        }

        private ActiveMatch GetActiveMatch(int matchId)
        {
            if (matchesById.ContainsKey(matchId))
            {
                return matchesById[matchId];
            }
            throw new MatchNotFoundException("The active match is not available on this server.");
        }

        private ActiveMatch GetAuthorizedMatch(MatchParticipantSession participantSession)
        {
            ActiveMatch activeMatch = GetActiveMatch(participantSession.MatchId);
            MatchParticipant? participant = activeMatch.Participants
                .FirstOrDefault(player => player.UserId == participantSession.UserId);
            if (participant == null)
            {
                throw new InvalidSessionException("The player does not belong to the requested match.");
            }
            if (participant.UserId >= MinimumValidIdentifier)
            {
                if (sessionRegistry.GetUserId(participantSession.SessionToken) != participant.UserId)
                {
                    throw new InvalidSessionException("The session does not belong to the requested participant.");
                }
            }
            else if (!participant.IsGuest || participant.UserId == InvalidPlayerIdentifier)
            {
                throw new InvalidSessionException("A valid guest participant is required.");
            }
            if (RefreshMatch(activeMatch))
            {
                StoreCompletion(activeMatch);
                BroadcastBoardState(participantSession.MatchId);
            }
            return activeMatch;
        }

        public int GetFinishedLobbyId(MatchParticipantSession participant)
        {
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetAuthorizedMatch(participant);
                if (activeMatch.GetParticipant(participant.UserId).HasAbandoned)
                {
                    throw new MatchActionException("An abandoned participant cannot return to the original lobby.");
                }
                if (!activeMatch.IsFinished)
                {
                    throw new MatchActionException("The match has not finished yet.");
                }
                StoreCompletion(activeMatch);
                return activeMatch.LobbyId;
            }
        }

        public BoardState RollDice(MatchParticipantSession participant, long turnId, long expectedRollId)
        {
            BoardState boardState;
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetAuthorizedMatch(participant);
                ValidateTurn(activeMatch, participant.UserId, turnId);
                activeMatch.ApplyRoll(DiceRoller.RollFourDice(), expectedRollId);
                boardState = BuildBoardState(activeMatch);
            }
            BroadcastBoardState(participant.MatchId);
            return boardState;
        }

        public BoardState ChooseDicePair(MatchParticipantSession participant, long turnId, DicePairSelection selection)
        {
            BoardState boardState;
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetAuthorizedMatch(participant);
                ValidateTurn(activeMatch, participant.UserId, turnId);
                activeMatch.ChoosePair(selection.RollId, selection.PairIndex, selection.PreferredColumn);
                boardState = BuildBoardState(activeMatch);
            }
            BroadcastBoardState(participant.MatchId);
            return boardState;
        }

        public BoardState StopTurn(MatchParticipantSession participant, long turnId)
        {
            BoardState boardState;
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetAuthorizedMatch(participant);
                bool canRetryCompletion = CanRetryCompletion(activeMatch, turnId, participant.UserId);
                if (!canRetryCompletion)
                {
                    ValidateTurn(activeMatch, participant.UserId, turnId);
                    activeMatch.StopTurn(turnId);
                }
                StoreCompletion(activeMatch);
                boardState = BuildBoardState(activeMatch);
            }
            BroadcastBoardState(participant.MatchId);
            return boardState;
        }

        private static void ValidateTurn(ActiveMatch activeMatch, int userId, long turnId)
        {
            MatchParticipant participant = activeMatch.GetParticipant(userId);
            bool activeParticipant = participant.IsConnected && !participant.HasAbandoned;
            bool isExpectedTurn = activeMatch.CurrentTurnUserId == userId && activeMatch.TurnId == turnId;
            bool isValidTurn = activeParticipant && isExpectedTurn;
            if (!isValidTurn)
            {
                throw new MatchActionException("Only the current player can roll or choose a pair.");
            }
        }

        public void ExpireTurns()
        {
            List<int> changedMatchIds = new List<int>();
            lock (matchLock)
            {
                foreach (ActiveMatch match in matchesById.Values.Where(candidate =>
                    !candidate.IsFinished || !storedCompletions.Contains(candidate.MatchId)))
                {
                    if (ProcessTimedMatch(match))
                    {
                        changedMatchIds.Add(match.MatchId);
                    }
                }
            }
            foreach (int matchId in changedMatchIds)
            {
                BroadcastBoardState(matchId);
            }
        }

        private bool RefreshMatch(ActiveMatch match)
        {
            long previousRevision = match.Revision;
            match.ProcessConnectionTimeouts();
            match.ExpireTurnIfNeeded();
            foreach (MatchParticipant player in match.Participants.Where(player => player.HasAbandoned))
            {
                AbandonedMatchPlayer abandoned = new AbandonedMatchPlayer(match.MatchId, match.LobbyId, player.UserId);
                if (queuedAbandonments.Add(abandoned))
                {
                    pendingAbandonments.Enqueue(abandoned);
                }
            }
            return previousRevision != match.Revision;
        }

        private bool ProcessTimedMatch(ActiveMatch match)
        {
            bool changed = RefreshMatch(match);
            bool pendingCompletion = match.IsFinished && !storedCompletions.Contains(match.MatchId);
            if (pendingCompletion)
            {
                bool completionSaved = RetryCompletion(match);
                changed = changed || completionSaved;
            }
            return changed;
        }

        private bool RetryCompletion(ActiveMatch match)
        {
            bool saved = false;
            bool canAttempt = !failedCompletionAttempts.ContainsKey(match.MatchId);
            if (!canAttempt)
            {
                canAttempt = timeProvider.GetElapsedTime(failedCompletionAttempts[match.MatchId]).TotalSeconds
                    >= CompletionRetrySeconds;
            }
            if (canAttempt)
            {
                try
                {
                    StoreCompletion(match);
                    failedCompletionAttempts.Remove(match.MatchId);
                    saved = true;
                }
                catch (DataOperationException dataOperationException)
                {
                    failedCompletionAttempts[match.MatchId] = timeProvider.GetTimestamp();
                    JsonLogger.Error(dataOperationException, "Could not persist a match completed by disconnection.");
                }
            }
            return saved;
        }

        public IReadOnlyList<AbandonedMatchPlayer> TakeAbandonedParticipants()
        {
            List<AbandonedMatchPlayer> abandoned = new List<AbandonedMatchPlayer>();
            lock (matchLock)
            {
                while (pendingAbandonments.Count > EmptyQueue)
                {
                    abandoned.Add(pendingAbandonments.Dequeue());
                }
            }
            return abandoned;
        }

        public BoardState LeaveMatch(MatchParticipantSession participant)
        {
            BoardState snapshot;
            lock (matchLock)
            {
                ActiveMatch match = GetAuthorizedMatch(participant);
                match.AbandonParticipant(participant.UserId);
                RefreshMatch(match);
                StoreCompletion(match);
                snapshot = BuildBoardState(match);
            }
            BroadcastBoardState(participant.MatchId);
            return snapshot;
        }

        private bool CanRetryCompletion(ActiveMatch match, long turnId, int userId)
        {
            bool hasPendingCompletion = match.IsFinished && !storedCompletions.Contains(match.MatchId);
            bool isWinnerRetry = userId == match.WinnerUserId && turnId == match.TurnId - CompletedTurnOffset;
            return hasPendingCompletion && isWinnerRetry;
        }

        private void StoreCompletion(ActiveMatch match)
        {
            bool shouldStore = match.IsFinished && !storedCompletions.Contains(match.MatchId);
            if (shouldStore)
            {
                completedMatchRepository.SaveCompletedMatch(match);
                storedCompletions.Add(match.MatchId);
            }
        }

        private void BroadcastBoardState(int matchId)
        {
            BoardState boardState;
            List<KeyValuePair<int, IServerStreamWriter<BoardState>>> subscribers;
            lock (matchLock)
            {
                boardState = BuildBoardState(GetActiveMatch(matchId));
                subscribers = subscribersByMatchId[matchId].ToList();
            }
            foreach (KeyValuePair<int, IServerStreamWriter<BoardState>> subscriber in subscribers)
            {
                Task.Run(() => WriteBoardState(subscriber.Value, boardState, subscriber.Key));
            }
        }

        private BoardState BuildBoardState(ActiveMatch activeMatch)
        {
            BoardState boardState = new BoardState
            {
                MatchId = activeMatch.MatchId,
                CurrentTurnUserId = activeMatch.CurrentTurnUserId,
                RulesVariant = activeMatch.RulesVariant,
                TurnDurationSeconds = activeMatch.TurnDurationSeconds,
                Revision = activeMatch.Revision,
                RollId = activeMatch.RollId,
                AwaitingPairChoice = activeMatch.AwaitingPairChoice,
                SelectedPairIndex = activeMatch.SelectedPairIndex,
                MaximumTemporaryMarkers = activeMatch.TurnProgress.MaximumTemporaryMarkers,
                TurnId = activeMatch.TurnId,
                LastTurnUserId = activeMatch.LastTurnUserId,
                LastTurnBusted = activeMatch.LastTurnBusted,
                CanStop = activeMatch.CanStop,
                IsFinished = activeMatch.IsFinished,
                WinnerUserId = activeMatch.WinnerUserId,
                ColumnsToWin = activeMatch.ColumnsToWin,
                TurnRemainingMilliseconds = (long)Math.Ceiling(activeMatch.GetRemainingTurnTime().TotalMilliseconds),
                LastTurnTimedOut = activeMatch.LastTurnTimedOut,
                EndReason = activeMatch.EndReason,
                ReconnectionGraceSeconds = MatchParticipant.ReconnectionGraceSeconds
            };
            foreach (KeyValuePair<int, int> progress in activeMatch.TurnProgress.StepsByColumn)
            {
                boardState.TemporaryStepsByColumn.Add(progress.Key, progress.Value);
            }
            boardState.Dice.AddRange(activeMatch.Dice);
            foreach (DicePairCombination combination in activeMatch.PairOptions)
            {
                PairOption pairOption = new PairOption
                {
                    PairIndex = combination.PairIndex,
                    FirstSum = combination.FirstSum,
                    SecondSum = combination.SecondSum,
                    IsPlayable = activeMatch.TurnProgress.IsPlayable(combination),
                    RequiresColumnChoice = activeMatch.TurnProgress.RequiresColumnChoice(combination)
                };
                pairOption.FirstPairDice.AddRange(combination.FirstPairDice);
                pairOption.SecondPairDice.AddRange(combination.SecondPairDice);
                boardState.PairOptions.Add(pairOption);
            }
            foreach (MatchParticipant participant in activeMatch.Participants)
            {
                boardState.Players.Add(new BoardPlayer
                {
                    UserId = participant.UserId, Nickname = participant.Nickname,
                    Color = participant.Color, IsConnected = participant.IsConnected,
                    HasAbandoned = participant.HasAbandoned,
                    ReconnectionSecondsRemaining = participant.ReconnectionSecondsRemaining,
                    ClaimedColumns = activeMatch.Columns.Count(column =>
                        column.IsClaimed && column.OwnerUserId == participant.UserId)
                });
            }
            foreach (MatchColumn column in activeMatch.Columns)
            {
                BoardColumn boardColumn = new BoardColumn
                {
                    ColumnNumber = column.ColumnNumber, MaximumSteps = column.MaximumSteps,
                    IsClaimed = column.IsClaimed, OwnerUserId = column.OwnerUserId
                };
                foreach (KeyValuePair<int, int> progress in column.StepsByUserId)
                {
                    boardColumn.StepsByUserId.Add(progress.Key, progress.Value);
                }
                boardState.Columns.Add(boardColumn);
            }
            return boardState;
        }

        private void WriteBoardState(IServerStreamWriter<BoardState> responseStream, BoardState boardState,
            int subscriberUserId)
        {
            try
            {
                lock (responseStream)
                {
                    responseStream.WriteAsync(boardState).GetAwaiter().GetResult();
                }
            }
            catch (RpcException rpcException)
            {
                bool disconnected = DisconnectSubscriber(boardState.MatchId, subscriberUserId, responseStream);
                if (disconnected && rpcException.StatusCode != StatusCode.Cancelled)
                {
                    JsonLogger.Error(rpcException, "Could not deliver the match snapshot.");
                }
            }
            catch (InvalidOperationException invalidOperationException)
            {
                if (DisconnectSubscriber(boardState.MatchId, subscriberUserId, responseStream))
                {
                    JsonLogger.Error(invalidOperationException, "The match stream cannot accept the snapshot.");
                }
            }
            catch (IOException ioException)
            {
                if (DisconnectSubscriber(boardState.MatchId, subscriberUserId, responseStream))
                {
                    JsonLogger.Error(ioException, "The match stream disconnected while sending the snapshot.");
                }
            }
            catch (OperationCanceledException canceledException)
            {
                bool disconnected = DisconnectSubscriber(boardState.MatchId, subscriberUserId, responseStream);
                if (disconnected && !canceledException.CancellationToken.IsCancellationRequested)
                {
                    JsonLogger.Error(canceledException, "The match snapshot write was cancelled unexpectedly.");
                }
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
