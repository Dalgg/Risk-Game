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
        private readonly object matchLock = new object();
        private readonly Dictionary<int, ActiveMatch> matchesById = new Dictionary<int, ActiveMatch>();
        private readonly Dictionary<int, Dictionary<int, IServerStreamWriter<BoardState>>> subscribersByMatchId =
            new Dictionary<int, Dictionary<int, IServerStreamWriter<BoardState>>>();
        private readonly ProfileSessionRegistry sessionRegistry;

        public MatchSessionManager(ProfileSessionRegistry sessionRegistry)
        {
            this.sessionRegistry = sessionRegistry;
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
                ActiveMatch activeMatch = new ActiveMatch(matchId, lobbyDetails);
                matchesById.Add(matchId, activeMatch);
                subscribersByMatchId.Add(matchId, new Dictionary<int, IServerStreamWriter<BoardState>>());
                return activeMatch;
            }
        }

        public Task SubscribeMatchEvents(MatchSubscription subscription,
            IServerStreamWriter<BoardState> responseStream, CancellationToken cancellationToken)
        {
            RegisterSubscriber(subscription, responseStream);
            try
            {
                BroadcastBoardState(subscription.MatchId);
                cancellationToken.WaitHandle.WaitOne();
                return Task.CompletedTask;
            }
            finally
            {
                UnregisterSubscriber(subscription, responseStream);
            }
        }

        private void RegisterSubscriber(MatchSubscription subscription, IServerStreamWriter<BoardState> responseStream)
        {
            lock (matchLock)
            {
                ActiveMatch activeMatch = GetActiveMatch(subscription.MatchId);
                MatchParticipant? participant = activeMatch.Participants
                    .FirstOrDefault(player => player.UserId == subscription.UserId);
                if (participant == null)
                {
                    throw new InvalidSessionException("The player does not belong to the requested match.");
                }
                if (participant.UserId >= MinimumValidIdentifier)
                {
                    if (sessionRegistry.GetUserId(subscription.SessionToken) != participant.UserId)
                    {
                        throw new InvalidSessionException("The session does not belong to the requested participant.");
                    }
                }
                else if (!participant.IsGuest || participant.UserId == InvalidPlayerIdentifier)
                {
                    throw new InvalidSessionException("A valid guest participant is required.");
                }
                subscribersByMatchId[subscription.MatchId][subscription.UserId] = responseStream;
                activeMatch.UpdateConnection(subscription.UserId, true);
            }
        }

        private void UnregisterSubscriber(MatchSubscription subscription,
            IServerStreamWriter<BoardState> responseStream)
        {
            bool subscriptionRemoved = false;
            lock (matchLock)
            {
                Dictionary<int, IServerStreamWriter<BoardState>> subscribers =
                    subscribersByMatchId[subscription.MatchId];
                if (subscribers.ContainsKey(subscription.UserId)
                    && ReferenceEquals(subscribers[subscription.UserId], responseStream))
                {
                    subscribers.Remove(subscription.UserId);
                    matchesById[subscription.MatchId].UpdateConnection(subscription.UserId, false);
                    subscriptionRemoved = true;
                }
            }
            if (subscriptionRemoved)
            {
                BroadcastBoardState(subscription.MatchId);
            }
        }

        private ActiveMatch GetActiveMatch(int matchId)
        {
            if (matchesById.ContainsKey(matchId))
            {
                return matchesById[matchId];
            }
            throw new MatchNotFoundException("The active match is not available on this server.");
        }

        private void BroadcastBoardState(int matchId)
        {
            BoardState boardState;
            List<IServerStreamWriter<BoardState>> subscribers;
            lock (matchLock)
            {
                boardState = BuildBoardState(GetActiveMatch(matchId));
                subscribers = subscribersByMatchId[matchId].Values.ToList();
            }
            foreach (IServerStreamWriter<BoardState> subscriber in subscribers)
            {
                Task.Run(() => WriteBoardState(subscriber, boardState));
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
                Revision = activeMatch.Revision
            };
            foreach (MatchParticipant participant in activeMatch.Participants)
            {
                boardState.Players.Add(new BoardPlayer
                {
                    UserId = participant.UserId, Nickname = participant.Nickname,
                    Color = participant.Color, IsConnected = participant.IsConnected
                });
            }
            foreach (MatchColumn column in activeMatch.Columns)
            {
                BoardColumn boardColumn = new BoardColumn
                {
                    ColumnNumber = column.ColumnNumber, MaximumSteps = column.MaximumSteps
                };
                foreach (KeyValuePair<int, int> progress in column.StepsByUserId)
                {
                    boardColumn.StepsByUserId.Add(progress.Key, progress.Value);
                }
                boardState.Columns.Add(boardColumn);
            }
            return boardState;
        }

        private void WriteBoardState(IServerStreamWriter<BoardState> responseStream, BoardState boardState)
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
                JsonLogger.Error(rpcException, "Could not deliver the match snapshot.");
            }
            catch (InvalidOperationException invalidOperationException)
            {
                JsonLogger.Error(invalidOperationException, "The match stream cannot accept the snapshot.");
            }
            catch (IOException ioException)
            {
                JsonLogger.Error(ioException, "The match stream disconnected while sending the snapshot.");
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
