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
        private readonly object syncLock = new object();
        private readonly Dictionary<string, ActiveLobbyRoom> roomsByCode = new Dictionary<string, ActiveLobbyRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> userToLobbyCode = new Dictionary<int, string>();
        private readonly LobbyRepositoryService lobbyRepository;

        public LobbySessionManager(LobbyRepositoryService lobbyRepository)
        {
            this.lobbyRepository = lobbyRepository;
        }

        public LobbyDetails CreateLobby(LobbyRules requestedRules, PlayerSummary hostPlayer)
        {
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
                RemovePlayerFromCurrentLobbyInternal(joiningPlayer.UserId);

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

            LobbyChanged joinedLobbyChange = new LobbyChanged
            {
                Change = LobbyChangeType.PlayerJoined,
                Lobby = lobbySnapshot,
                AffectedUserId = joiningPlayer.UserId
            };
            BroadcastLobbyChanged(subscribersToNotify, joinedLobbyChange);
            return lobbySnapshot;
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
                LobbyChanged leftLobbyChange = new LobbyChanged
                {
                    Change = LobbyChangeType.PlayerLeft,
                    Lobby = lobbySnapshot,
                    AffectedUserId = leavingUserId
                };
                BroadcastLobbyChanged(subscribersToNotify, leftLobbyChange);
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

        public void UnregisterSubscriber(int subscriberUserId)
        {
            LeaveLobby(subscriberUserId);
        }

        public int StartMatch(int requestingUserId)
        {
            int targetLobbyId;
            string targetLobbyCode;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify;

            lock (syncLock)
            {
                if (!userToLobbyCode.ContainsKey(requestingUserId))
                {
                    throw new LobbyNotFoundException("The player is not currently in an active lobby.");
                }

                targetLobbyCode = userToLobbyCode[requestingUserId];
                if (!roomsByCode.ContainsKey(targetLobbyCode))
                {
                    throw new LobbyNotFoundException("The lobby does not exist or is already closed.");
                }

                ActiveLobbyRoom activeLobbyRoom = roomsByCode[targetLobbyCode];
                if (activeLobbyRoom.HostUserId != requestingUserId)
                {
                    throw new InvalidOperationException("Only the lobby host can start the match.");
                }

                if (activeLobbyRoom.Players.Count < MinimumPlayersToStart)
                {
                    throw new InvalidOperationException("At least 2 players are required to start the match.");
                }

                targetLobbyId = activeLobbyRoom.LobbyId;
                subscribersToNotify = activeLobbyRoom.Subscribers.Values.ToList();
            }

            int createdMatchId = lobbyRepository.CreateMatchInDatabase(targetLobbyId);

            lock (syncLock)
            {
                if (roomsByCode.ContainsKey(targetLobbyCode))
                {
                    ActiveLobbyRoom activeLobbyRoom = roomsByCode[targetLobbyCode];
                    activeLobbyRoom.Status = LobbyStatus.InMatch;
                }
            }

            LobbyEvent matchStartingEvent = new LobbyEvent
            {
                MatchStarting = new MatchStarting
                {
                    MatchId = createdMatchId
                }
            };

            BroadcastLobbyEvent(subscribersToNotify, matchStartingEvent);
            return createdMatchId;
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

        public void UnregisterChatSubscriber(int subscriberUserId)
        {
            lock (syncLock)
            {
                if (userToLobbyCode.ContainsKey(subscriberUserId))
                {
                    string assignedLobbyCode = userToLobbyCode[subscriberUserId];
                    if (roomsByCode.ContainsKey(assignedLobbyCode))
                    {
                        ActiveLobbyRoom activeLobbyRoom = roomsByCode[assignedLobbyCode];
                        activeLobbyRoom.ChatSubscribers.Remove(subscriberUserId);
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
                Scope = ChatScope.Lobby,
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
                    bool isOnline = userToLobbyCode.ContainsKey(friendUser.IdUser);

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
                Status = activeLobbyRoom.Status
            };

            foreach (LobbyPlayer registeredPlayer in activeLobbyRoom.Players)
            {
                lobbyDetails.Players.Add(registeredPlayer.Clone());
            }

            return lobbyDetails;
        }

        private static void BroadcastLobbyChanged(
            IReadOnlyList<IServerStreamWriter<LobbyEvent>> subscribers,
            LobbyChanged lobbyChanged)
        {
            LobbyEvent notificationEvent = new LobbyEvent
            {
                LobbyChanged = lobbyChanged
            };

            BroadcastLobbyEvent(subscribers, notificationEvent);
        }

        private static void BroadcastLobbyEvent(
            IReadOnlyList<IServerStreamWriter<LobbyEvent>> subscribers,
            LobbyEvent notificationEvent)
        {
            foreach (IServerStreamWriter<LobbyEvent> subscriberStream in subscribers)
            {
                Task.Run(() =>
                {
                    try
                    {
                        subscriberStream.WriteAsync(notificationEvent).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException invalidOperationException)
                    {
                        JsonLogger.Error(invalidOperationException, "Could not write lobby notification to subscriber stream.");
                    }
                    catch (RpcException rpcException)
                    {
                        JsonLogger.Error(rpcException, "Could not write lobby notification to subscriber stream.");
                    }
                });
            }
        }

        private static void BroadcastChatMessage(
            IReadOnlyList<IServerStreamWriter<ChatMessage>> chatSubscribers,
            ChatMessage outgoingMessage)
        {
            foreach (IServerStreamWriter<ChatMessage> chatSubscriberStream in chatSubscribers)
            {
                Task.Run(() =>
                {
                    try
                    {
                        chatSubscriberStream.WriteAsync(outgoingMessage).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException invalidOperationException)
                    {
                        JsonLogger.Error(invalidOperationException, "Could not write chat message to subscriber stream.");
                    }
                    catch (RpcException rpcException)
                    {
                        JsonLogger.Error(rpcException, "Could not write chat message to subscriber stream.");
                    }
                });
            }
        }

        private sealed class ActiveLobbyRoom
        {
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
