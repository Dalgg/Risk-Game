using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Services;
using Lobby = RiskGame.Models.Lobby;

namespace RiskGame.Server.Services
{
    public sealed class LobbySessionManager
    {
        private const int MaxPlayersPerLobby = 4;
        private const int MinimumPlayersToStart = 2;
        private const int DefaultTurnDurationSeconds = 60;
        private const int MaxChatMessageLength = 120;
        private const string FastRulesVariantName = "Fast";
        private const string StandardRulesVariantName = "Standard";
        private const int MinimumValidIdentifier = 1;
        private const int NoPendingReturns = 0;
        private const int InvalidUserIdentifier = 0;
        private readonly object syncLock = new object();
        private readonly Dictionary<string, ActiveLobbyRoom> roomsByCode = new Dictionary<string, ActiveLobbyRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> userToLobbyCode = new Dictionary<int, string>();
        private readonly LobbyRepositoryService lobbyRepository;
        private readonly ProfileSessionRegistry sessionRegistry;        
        private readonly MatchSessionManager matchSessionManager;

        public LobbySessionManager(LobbyRepositoryService lobbyRepository, ProfileSessionRegistry sessionRegistry,
            MatchSessionManager matchSessionManager)
        {
            this.lobbyRepository = lobbyRepository;
            this.sessionRegistry = sessionRegistry;
            this.matchSessionManager = matchSessionManager;
        }

        public LobbyDetails CreateLobby(LobbyRules requestedRules, PlayerSummary hostPlayer)
        {
            LobbyRulesValidator.Validate(requestedRules);
            string rulesVariantName = StandardRulesVariantName;
            if (requestedRules != null && requestedRules.RulesVariant == RulesVariant.Fast)
            {
                rulesVariantName = FastRulesVariantName;
            }

            int turnDurationSeconds = DefaultTurnDurationSeconds;
            if (requestedRules != null && requestedRules.TurnDurationSeconds > 0)
            {
                turnDurationSeconds = requestedRules.TurnDurationSeconds;
            }

            Lobby createdDatabaseLobby = lobbyRepository.CreateLobbyInDatabase(
                rulesVariantName,
                turnDurationSeconds);

            return JoinLobby(createdDatabaseLobby.LobbyCode, hostPlayer);
        }

        public void ValidateParticipant(int userId, string sessionToken)
        {
            if (userId >= MinimumValidIdentifier)
            {
                sessionRegistry.ValidateUser(userId, sessionToken);
            }
            else if (userId == InvalidUserIdentifier)
            {
                throw new InvalidSessionException("A valid player identifier is required.");
            }
        }

        public bool IsLobbySubscriberCurrent(int userId, IServerStreamWriter<LobbyEvent> stream)
        {
            bool current = false;
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(userId) && roomsByCode.ContainsKey(userToLobbyCode[userId]))
                {
                    ActiveLobbyRoom room = roomsByCode[userToLobbyCode[userId]];
                    if (room.Subscribers.ContainsKey(userId))
                    {
                        current = ReferenceEquals(room.Subscribers[userId], stream);
                    }
                }
            }
            return current;
        }

        public bool IsChatSubscriberCurrent(int userId, IServerStreamWriter<ChatMessage> stream)
        {
            bool current = false;
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(userId) && roomsByCode.ContainsKey(userToLobbyCode[userId]))
                {
                    ActiveLobbyRoom room = roomsByCode[userToLobbyCode[userId]];
                    if (room.ChatSubscribers.ContainsKey(userId))
                    {
                        current = ReferenceEquals(room.ChatSubscribers[userId], stream);
                    }
                }
            }
            return current;
        }

        public LobbyDetails UpdateRules(int userId, string sessionToken, LobbyRules rules)
        {
            LobbyRulesValidator.Validate(rules);
            LobbyDetails snapshot;
            lock (syncLock)
            {
                ActiveLobbyRoom room = GetEditableRoom(userId, sessionToken);
                string variantName = StandardRulesVariantName;
                if (rules.RulesVariant == RulesVariant.Fast)
                {
                    variantName = FastRulesVariantName;
                }
                lobbyRepository.UpdateRules(room.LobbyId, variantName, rules.TurnDurationSeconds);
                room.Rules.RulesVariant = rules.RulesVariant;
                room.Rules.TurnDurationSeconds = rules.TurnDurationSeconds;
                snapshot = BuildLobbyDetails(room);
                BroadcastLobbySnapshot(room.Subscribers.Values.ToList(), snapshot);
            }
            return snapshot;
        }

        private ActiveLobbyRoom GetEditableRoom(int userId, string sessionToken)
        {
            if (!userToLobbyCode.ContainsKey(userId))
            {
                throw new InvalidOperationException("The player does not belong to an active lobby.");
            }
            ActiveLobbyRoom room = roomsByCode[userToLobbyCode[userId]];
            if (room.HostUserId != userId || room.Status != LobbyStatus.Waiting)
            {
                throw new InvalidOperationException("Only the host can change rules before the match starts.");
            }
            if (userId >= MinimumValidIdentifier
                && sessionRegistry.GetUserId(sessionToken) != userId)
            {
                throw new InvalidSessionException("The session does not belong to the lobby host.");
            }
            return room;
        }

        public LobbyDetails JoinLobby(string lobbyCode, PlayerSummary joiningPlayer)
        {
            string normalizedLobbyCode = string.Empty;
            if (lobbyCode != null)
            {
                normalizedLobbyCode = lobbyCode.Trim();
            }

            Lobby databaseLobby = lobbyRepository.GetActiveLobbyByCode(normalizedLobbyCode);
            LobbyDetails lobbySnapshot;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify;

            lock (syncLock)
            {
                ActiveLobbyRoom activeLobbyRoom;
                if (roomsByCode.ContainsKey(databaseLobby.LobbyCode))
                {
                    activeLobbyRoom = roomsByCode[databaseLobby.LobbyCode];
                }
                else
                {
                    activeLobbyRoom = CreateRoomFromEntity(databaseLobby, joiningPlayer.UserId);
                    roomsByCode[databaseLobby.LobbyCode] = activeLobbyRoom;
                }

                if (activeLobbyRoom.Status != LobbyStatus.Waiting)
                {
                    throw new LobbyNotFoundException("The lobby is no longer accepting players.");
                }
                RemovePlayerFromCurrentLobbyInternal(joiningPlayer.UserId);

                LobbyPlayer? existingLobbyPlayer = activeLobbyRoom.Players.FirstOrDefault(
                    registeredPlayer => registeredPlayer.Player.UserId == joiningPlayer.UserId);

                if (existingLobbyPlayer == null)
                {
                    if (activeLobbyRoom.Players.Count >= MaxPlayersPerLobby)
                    {
                        throw new LobbyFullException("The lobby has reached the maximum of 4 players.");
                    }

                    bool isRoomHost = activeLobbyRoom.Players.Count == 0;
                    if (isRoomHost)
                    {
                        activeLobbyRoom.HostUserId = joiningPlayer.UserId;
                    }

                    PlayerColor assignedPlayerColor = AssignAvailableColor(activeLobbyRoom.Players);
                    LobbyPlayer newLobbyPlayer = new LobbyPlayer
                    {
                        Player = joiningPlayer,
                        IsReady = false,
                        IsHost = isRoomHost,
                        Color = assignedPlayerColor
                    };

                    activeLobbyRoom.Players.Add(newLobbyPlayer);
                }

                userToLobbyCode[joiningPlayer.UserId] = activeLobbyRoom.LobbyCode;
                lobbySnapshot = BuildLobbyDetails(activeLobbyRoom);
                subscribersToNotify = activeLobbyRoom.Subscribers.Values.ToList();
            }

            BroadcastLobbySnapshot(subscribersToNotify, lobbySnapshot);
            return lobbySnapshot;
        }

        public LobbyDetails JoinInvitedLobby(int lobbyId, PlayerSummary joiningPlayer)
        {
            lock (syncLock)
            {
                ActiveLobbyRoom? activeLobbyRoom = roomsByCode.Values
                    .FirstOrDefault(room => room.LobbyId == lobbyId);
                if (activeLobbyRoom == null || activeLobbyRoom.Status != LobbyStatus.Waiting)
                {
                    throw new InvitationUnavailableException("The invited lobby is closed or has already started.");
                }
                if (activeLobbyRoom.Players.Count >= MaxPlayersPerLobby)
                {
                    throw new InvitationUnavailableException("The invited lobby is full.");
                }
                try
                {
                    return JoinLobby(activeLobbyRoom.LobbyCode, joiningPlayer);
                }
                catch (LobbyFullException)
                {
                    throw new InvitationUnavailableException("The invited lobby is full.");
                }
                catch (LobbyNotFoundException)
                {
                    throw new InvitationUnavailableException("The invited lobby is no longer available.");
                }
            }
        }

        public void LeaveLobby(int leavingUserId)
        {
            LobbyDetails? lobbySnapshot = null;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify = new List<IServerStreamWriter<LobbyEvent>>();

            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(leavingUserId))
                {
                    string assignedLobbyCode = userToLobbyCode[leavingUserId];
                    if (roomsByCode.ContainsKey(assignedLobbyCode))
                    {
                        ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                        RemovePlayerFromRoom(activeLobbyRoom, leavingUserId);
                        userToLobbyCode.Remove(leavingUserId);

                        if (activeLobbyRoom.Players.Count > 0)
                        {
                            lobbySnapshot = BuildLobbyDetails(activeLobbyRoom);
                            subscribersToNotify = activeLobbyRoom.Subscribers.Values.ToList();
                        }
                        else
                        {
                            roomsByCode.Remove(assignedLobbyCode);
                        }
                    }
                }
            }

            if (lobbySnapshot != null && subscribersToNotify.Count > 0)
            {
                BroadcastLobbySnapshot(subscribersToNotify, lobbySnapshot);
            }
        }

        public void RemoveAbandonedParticipant(AbandonedMatchPlayer abandoned)
        {
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(abandoned.UserId))
                {
                    TryRemoveAbandonedFromRoom(abandoned, userToLobbyCode[abandoned.UserId]);
                }
            }
        }

        private void TryRemoveAbandonedFromRoom(AbandonedMatchPlayer abandoned, string lobbyCode)
        {
            if (roomsByCode.ContainsKey(lobbyCode))
            {
                ActiveLobbyRoom room = roomsByCode[lobbyCode];
                if (room.LobbyId == abandoned.LobbyId && room.ActiveMatchId == abandoned.MatchId)
                {
                    LeaveLobby(abandoned.UserId);
                }
            }
        }

        public LobbyDetails RegisterSubscriber(int subscriberUserId, IServerStreamWriter<LobbyEvent> responseStream)
        {
            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(subscriberUserId))
                {
                    throw new LobbyNotFoundException("The player is not currently in an active lobby.");
                }

                string assignedLobbyCode = userToLobbyCode[subscriberUserId];
                if (!roomsByCode.ContainsKey(assignedLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby does not exist or is already closed.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                activeLobbyRoom.Subscribers[subscriberUserId] = responseStream;
                return BuildLobbyDetails(activeLobbyRoom);
            }
        }

        public void UnregisterSubscriber(int subscriberUserId, IServerStreamWriter<LobbyEvent> responseStream)
        {
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(subscriberUserId)
                    && roomsByCode.ContainsKey(userToLobbyCode[subscriberUserId]))
                {
                    ActiveLobbyRoom room = roomsByCode[userToLobbyCode[subscriberUserId]];
                    if (room.Subscribers.ContainsKey(subscriberUserId)
                        && ReferenceEquals(room.Subscribers[subscriberUserId], responseStream))
                    {
                        room.Subscribers.Remove(subscriberUserId);
                        if (room.Status == LobbyStatus.Waiting && !room.PendingReturns.Contains(subscriberUserId))
                        {
                            LeaveLobby(subscriberUserId);
                        }
                    }
                }
            }
        }

        public LobbyDetails ReturnToLobby(MatchParticipantSession participant)
        {
            int lobbyId = matchSessionManager.GetFinishedLobbyId(participant);
            LobbyDetails snapshot;
            List<IServerStreamWriter<LobbyEvent>> subscribers;
            lock (syncLock)
            {
                ActiveLobbyRoom? room = roomsByCode.Values.FirstOrDefault(candidate => candidate.LobbyId == lobbyId);
                bool isParticipantInRoom = false;
                if (room != null && room.ActiveMatchId == participant.MatchId)
                {
                    isParticipantInRoom = userToLobbyCode.ContainsKey(participant.UserId)
                        && userToLobbyCode[participant.UserId] == room.LobbyCode;
                }
                if (!isParticipantInRoom || room == null)
                {
                    throw new MatchActionException("The original lobby is no longer available for this participant.");
                }
                room.PendingReturns.Remove(participant.UserId);
                room.Status = LobbyStatus.Waiting;
                snapshot = BuildLobbyDetails(room);
                subscribers = room.Subscribers.Values.ToList();
            }
            BroadcastLobbySnapshot(subscribers, snapshot);
            return snapshot;
        }

        public int StartMatch(int requestingUserId)
        {
            ActiveMatch activeMatch;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify;

            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(requestingUserId))
                {
                    throw new LobbyNotFoundException("The player is not currently in an active lobby.");
                }

                string targetLobbyCode = userToLobbyCode[requestingUserId];
                if (!roomsByCode.ContainsKey(targetLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby does not exist or is already closed.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[targetLobbyCode];
                if (activeLobbyRoom.Status != LobbyStatus.Waiting)
                {
                    throw new InvalidOperationException("This lobby has already started its match.");
                }
                if (activeLobbyRoom.PendingReturns.Count != NoPendingReturns)
                {
                    throw new InvalidOperationException("Participants must return or leave before starting again.");
                }
                if (activeLobbyRoom.HostUserId != requestingUserId)
                {
                    throw new InvalidOperationException("Only the lobby host can start the match.");
                }

                if (activeLobbyRoom.Players.Count < MinimumPlayersToStart)
                {
                    throw new InvalidOperationException("At least 2 players are required to start the match.");
                }

                LobbyDetails initialLobbyDetails = BuildLobbyDetails(activeLobbyRoom);
                int createdMatchId = lobbyRepository.CreateMatchInDatabase(activeLobbyRoom.LobbyId);
                activeMatch = matchSessionManager.CreateMatch(createdMatchId, initialLobbyDetails);
                activeLobbyRoom.ActiveMatchId = activeMatch.MatchId;
                foreach (LobbyPlayer player in activeLobbyRoom.Players)
                {
                    activeLobbyRoom.PendingReturns.Add(player.Player.UserId);
                }
                activeLobbyRoom.Status = LobbyStatus.InMatch;
                subscribersToNotify = activeLobbyRoom.Subscribers.Values.ToList();
                activeLobbyRoom.ChatSubscribers.Clear();
            }

            LobbyEvent matchStartingEvent = new LobbyEvent
            {
                StartedMatchId = activeMatch.MatchId
            };

            BroadcastLobbyEvent(subscribersToNotify, matchStartingEvent);
            return activeMatch.MatchId;
        }

        public void RegisterChatSubscriber(int subscriberUserId, IServerStreamWriter<ChatMessage> responseStream)
        {
            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(subscriberUserId))
                {
                    throw new LobbyNotFoundException("The player is not currently in an active lobby.");
                }

                string assignedLobbyCode = userToLobbyCode[subscriberUserId];
                if (!roomsByCode.ContainsKey(assignedLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby does not exist or is already closed.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                activeLobbyRoom.ChatSubscribers[subscriberUserId] = responseStream;
            }
        }

        public void UnregisterChatSubscriber(int subscriberUserId, IServerStreamWriter<ChatMessage> responseStream)
        {
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(subscriberUserId))
                {
                    string assignedLobbyCode = userToLobbyCode[subscriberUserId];
                    if (roomsByCode.ContainsKey(assignedLobbyCode))
                    {
                        ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                        if (activeLobbyRoom.ChatSubscribers.ContainsKey(subscriberUserId)
                            && ReferenceEquals(activeLobbyRoom.ChatSubscribers[subscriberUserId], responseStream))
                        {
                            activeLobbyRoom.ChatSubscribers.Remove(subscriberUserId);
                        }
                    }
                }
            }
        }

        public void SendLobbyChatMessage(PlayerSummary senderPlayer, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                throw new ArgumentException("Chat message cannot be empty or whitespace.");
            }

            string trimmedMessageText = messageText.Trim();
            if (trimmedMessageText.Length > MaxChatMessageLength)
            {
                throw new ArgumentException("Chat message exceeds the maximum allowed length.");
            }

            List<IServerStreamWriter<ChatMessage>> chatSubscribersToNotify;

            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(senderPlayer.UserId))
                {
                    throw new LobbyNotFoundException("The sender is not currently in an active lobby.");
                }

                string assignedLobbyCode = userToLobbyCode[senderPlayer.UserId];
                if (!roomsByCode.ContainsKey(assignedLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby does not exist.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                chatSubscribersToNotify = activeLobbyRoom.ChatSubscribers.Values.ToList();
            }

            ChatMessage outgoingMessage = new ChatMessage
            {
                Text = trimmedMessageText,
                Sender = senderPlayer,
                SentAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            BroadcastChatMessage(chatSubscribersToNotify, outgoingMessage);
        }

        public FriendSummaryList GetFriends(int requestingUserId)
        {
            FriendSummaryList summaryList = new FriendSummaryList();
            if (requestingUserId < MinimumValidIdentifier)
            {
                return summaryList;
            }

            List<RiskGame.Models.User> friendUsers = lobbyRepository.GetAcceptedFriends(requestingUserId);

            lock (syncLock)
            {
                foreach (RiskGame.Models.User friendUser in friendUsers)
                {
                    bool isOnline = sessionRegistry.IsUserOnline(friendUser.IdUser);
                    string avatarReferenceText = string.Empty;
                    if (friendUser.AvatarReference != null)
                    {
                        avatarReferenceText = friendUser.AvatarReference;
                    }

                    FriendSummary friendSummary = new FriendSummary
                    {
                        UserId = friendUser.IdUser,
                        Nickname = friendUser.Nickname,
                        AvatarReference = avatarReferenceText,
                        IsOnline = isOnline
                    };

                    summaryList.Friends.Add(friendSummary);
                }
            }

            return summaryList;
        }

        public void InviteFriend(int senderUserId, int recipientUserId)
        {
            if (senderUserId < MinimumValidIdentifier || recipientUserId < MinimumValidIdentifier)
            {
                throw new ArgumentException("Identifiers must be positive numbers.");
            }

            int targetLobbyId;

            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(senderUserId))
                {
                    throw new LobbyNotFoundException("The sender is not currently in an active lobby room.");
                }

                string assignedLobbyCode = userToLobbyCode[senderUserId];
                if (!roomsByCode.ContainsKey(assignedLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby room is not available.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                targetLobbyId = activeLobbyRoom.LobbyId;
            }

            lobbyRepository.CreateLobbyInvitation(targetLobbyId, senderUserId, recipientUserId);
        }

        public void BroadcastPresenceUpdate()
        {
            lock (syncLock)
            {
                foreach (ActiveLobbyRoom activeLobbyRoom in roomsByCode.Values)
                {
                    LobbyDetails lobbySnapshot = BuildLobbyDetails(activeLobbyRoom);
                    List<IServerStreamWriter<LobbyEvent>> subscribersToNotify =
                        activeLobbyRoom.Subscribers.Values.ToList();
                    BroadcastLobbySnapshot(subscribersToNotify, lobbySnapshot);
                }
            }
        }

        private void RemovePlayerFromCurrentLobbyInternal(int targetUserId)
        {
            if (userToLobbyCode.ContainsKey(targetUserId))
            {
                string currentLobbyCode = userToLobbyCode[targetUserId];
                if (roomsByCode.ContainsKey(currentLobbyCode))
                {
                    ActiveLobbyRoom currentLobbyRoom = roomsByCode[currentLobbyCode];
                    RemovePlayerFromRoom(currentLobbyRoom, targetUserId);
                    userToLobbyCode.Remove(targetUserId);
                }
            }
        }

        private static void RemovePlayerFromRoom(ActiveLobbyRoom activeLobbyRoom, int targetUserId)
        {
            activeLobbyRoom.PendingReturns.Remove(targetUserId);
            activeLobbyRoom.Subscribers.Remove(targetUserId);
            activeLobbyRoom.ChatSubscribers.Remove(targetUserId);
            LobbyPlayer? matchingLobbyPlayer = activeLobbyRoom.Players.FirstOrDefault(
                registeredPlayer => registeredPlayer.Player.UserId == targetUserId);

            if (matchingLobbyPlayer != null)
            {
                activeLobbyRoom.Players.Remove(matchingLobbyPlayer);
            }

            bool hasActiveHost = activeLobbyRoom.Players.Any(registeredPlayer => registeredPlayer.IsHost);
            if (activeLobbyRoom.Players.Count > 0 && !hasActiveHost)
            {
                activeLobbyRoom.Players[0].IsHost = true;
                activeLobbyRoom.HostUserId = activeLobbyRoom.Players[0].Player.UserId;
            }
        }

        private static ActiveLobbyRoom CreateRoomFromEntity(Lobby databaseLobby, int initialHostUserId)
        {
            RulesVariant selectedRulesVariant = RulesVariant.Standard;
            if (string.Equals(databaseLobby.RulesVariant, FastRulesVariantName, StringComparison.OrdinalIgnoreCase))
            {
                selectedRulesVariant = RulesVariant.Fast;
            }

            return new ActiveLobbyRoom
            {
                LobbyId = databaseLobby.IdLobby,
                LobbyCode = databaseLobby.LobbyCode,
                HostUserId = initialHostUserId,
                Status = LobbyStatus.Waiting,
                Rules = new LobbyRules
                {
                    RulesVariant = selectedRulesVariant,
                    TurnDurationSeconds = databaseLobby.TurnDuration,
                    MaxPlayers = MaxPlayersPerLobby,
                    IsPrivate = false
                }
            };
        }

        private static PlayerColor AssignAvailableColor(IReadOnlyList<LobbyPlayer> existingPlayers)
        {
            PlayerColor[] availableColors = new[]
            {
                PlayerColor.Red,
                PlayerColor.Blue,
                PlayerColor.Green,
                PlayerColor.Yellow
            };

            foreach (PlayerColor candidateColor in availableColors)
            {
                bool isColorTaken = existingPlayers.Any(registeredPlayer => registeredPlayer.Color == candidateColor);
                if (!isColorTaken)
                {
                    return candidateColor;
                }
            }

            return PlayerColor.Red;
        }

        private static LobbyDetails BuildLobbyDetails(ActiveLobbyRoom activeLobbyRoom)
        {
            LobbyDetails lobbyDetails = new LobbyDetails
            {
                LobbyId = activeLobbyRoom.LobbyId,
                LobbyCode = activeLobbyRoom.LobbyCode,
                HostUserId = activeLobbyRoom.HostUserId,
                Rules = activeLobbyRoom.Rules.Clone(),
                Status = activeLobbyRoom.Status,
                CanStartMatch = activeLobbyRoom.Status == LobbyStatus.Waiting
                    && activeLobbyRoom.PendingReturns.Count == NoPendingReturns
                    && activeLobbyRoom.Players.Count >= MinimumPlayersToStart
            };

            foreach (LobbyPlayer registeredPlayer in activeLobbyRoom.Players)
            {
                lobbyDetails.Players.Add(registeredPlayer.Clone());
            }

            return lobbyDetails;
        }

        private static void BroadcastLobbySnapshot(
            IReadOnlyList<IServerStreamWriter<LobbyEvent>> subscribers,
            LobbyDetails lobbySnapshot)
        {
            LobbyEvent notificationEvent = new LobbyEvent
            {
                Lobby = lobbySnapshot
            };

            BroadcastLobbyEvent(subscribers, notificationEvent);
        }

        private static void BroadcastLobbyEvent(
            IReadOnlyList<IServerStreamWriter<LobbyEvent>> subscribers,
            LobbyEvent notificationEvent)
        {
            foreach (IServerStreamWriter<LobbyEvent> subscriberStream in subscribers)
            {
                Task.Run(() => WriteLobbyEvent(subscriberStream, notificationEvent));
            }
        }

        private static void WriteLobbyEvent(IServerStreamWriter<LobbyEvent> subscriberStream, LobbyEvent notificationEvent)
        {
            try
            {
                lock (subscriberStream)
                {
                    subscriberStream.WriteAsync(notificationEvent).GetAwaiter().GetResult();
                }
            }
            catch (InvalidOperationException invalidOperationException)
            {
                JsonLogger.Error(invalidOperationException, "Could not write lobby notification to subscriber stream.");
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not write lobby notification to subscriber stream.");
            }
        }

        private static void BroadcastChatMessage(
            IReadOnlyList<IServerStreamWriter<ChatMessage>> chatSubscribers,
            ChatMessage outgoingMessage)
        {
            foreach (IServerStreamWriter<ChatMessage> chatSubscriberStream in chatSubscribers)
            {
                Task.Run(() => WriteChatMessage(chatSubscriberStream, outgoingMessage));
            }
        }

        private static void WriteChatMessage(IServerStreamWriter<ChatMessage> chatSubscriberStream,
            ChatMessage outgoingMessage)
        {
            try
            {
                lock (chatSubscriberStream)
                {
                    chatSubscriberStream.WriteAsync(outgoingMessage).GetAwaiter().GetResult();
                }
            }
            catch (InvalidOperationException invalidOperationException)
            {
                JsonLogger.Error(invalidOperationException, "Could not write chat message to subscriber stream.");
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not write chat message to subscriber stream.");
            }
        }

        private sealed class ActiveLobbyRoom
        {
            public int ActiveMatchId { get; set; }

            public HashSet<int> PendingReturns { get; } = new HashSet<int>();

            public int LobbyId { get; init; }

            public string LobbyCode { get; init; } = string.Empty;

            public int HostUserId { get; set; }

            public LobbyStatus Status { get; set; } = LobbyStatus.Waiting;

            public LobbyRules Rules { get; init; } = new LobbyRules();

            public List<LobbyPlayer> Players { get; } = new List<LobbyPlayer>();

            public Dictionary<int, IServerStreamWriter<LobbyEvent>> Subscribers { get; } = new Dictionary<int, IServerStreamWriter<LobbyEvent>>();

            public Dictionary<int, IServerStreamWriter<ChatMessage>> ChatSubscribers { get; } = new Dictionary<int, IServerStreamWriter<ChatMessage>>();
        }
    }
}
