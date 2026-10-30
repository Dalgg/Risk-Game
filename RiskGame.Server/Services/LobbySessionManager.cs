using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class LobbySessionManager
    {
        private const int MaxPlayersPerLobby = 4;
        private readonly object _syncLock = new object();
        private readonly Dictionary<string, ActiveLobbyRoom> _roomsByCode = new Dictionary<string, ActiveLobbyRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _userToLobbyCode = new Dictionary<int, string>();
        private readonly LobbyRepositoryService _lobbyRepository;

        public LobbySessionManager(LobbyRepositoryService lobbyRepository)
        {
            _lobbyRepository = lobbyRepository;
        }

        public LobbyDetails JoinLobby(string lobbyCode, PlayerSummary player)
        {
            string normalizedCode = (lobbyCode ?? string.Empty).Trim();
            RiskGame.Models.Lobby? dbLobby = _lobbyRepository.GetActiveLobbyByCode(normalizedCode);
            if (dbLobby == null)
            {
                throw new LobbyNotFoundException("The requested lobby code does not exist or is closed.");
            }

            LobbyDetails snapshot;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify;

            lock (_syncLock)
            {
                RemovePlayerFromCurrentLobbyInternal(player.UserId);

                if (!_roomsByCode.TryGetValue(dbLobby.LobbyCode, out ActiveLobbyRoom? room))
                {
                    room = CreateRoomFromEntity(dbLobby, player.UserId);
                    _roomsByCode[dbLobby.LobbyCode] = room;
                }

                LobbyPlayer? existingPlayer = room.Players.FirstOrDefault(candidate => candidate.Player.UserId == player.UserId);
                if (existingPlayer == null)
                {
                    if (room.Players.Count >= MaxPlayersPerLobby)
                    {
                        throw new LobbyFullException("The lobby has reached the maximum of 4 players.");
                    }

                    bool isHost = room.Players.Count == 0;
                    if (isHost)
                    {
                        room.HostUserId = player.UserId;
                    }

                    PlayerColor assignedColor = AssignAvailableColor(room.Players);
                    LobbyPlayer newLobbyPlayer = new LobbyPlayer
                    {
                        Player = player,
                        IsReady = false,
                        IsHost = isHost,
                        Color = assignedColor
                    };

                    room.Players.Add(newLobbyPlayer);
                }

                _userToLobbyCode[player.UserId] = room.LobbyCode;
                snapshot = BuildLobbyDetails(room);
                subscribersToNotify = room.Subscribers.Values.ToList();
            }

            BroadcastLobbyChanged(subscribersToNotify, LobbyChangeType.PlayerJoined, snapshot, player.UserId);
            return snapshot;
        }

        public void LeaveLobby(int userId)
        {
            LobbyDetails? snapshot = null;
            List<IServerStreamWriter<LobbyEvent>> subscribersToNotify = new List<IServerStreamWriter<LobbyEvent>>();

            lock (_syncLock)
            {
                if (_userToLobbyCode.TryGetValue(userId, out string? lobbyCode)
                    && _roomsByCode.TryGetValue(lobbyCode, out ActiveLobbyRoom? room))
                {
                    RemovePlayerFromRoom(room, userId);
                    _userToLobbyCode.Remove(userId);

                    if (room.Players.Count > 0)
                    {
                        snapshot = BuildLobbyDetails(room);
                        subscribersToNotify = room.Subscribers.Values.ToList();
                    }
                    else
                    {
                        _roomsByCode.Remove(lobbyCode);
                    }
                }
            }

            if (snapshot != null && subscribersToNotify.Count > 0)
            {
                BroadcastLobbyChanged(subscribersToNotify, LobbyChangeType.PlayerLeft, snapshot, userId);
            }
        }

        public LobbyDetails? RegisterSubscriber(int userId, IServerStreamWriter<LobbyEvent> responseStream)
        {
            lock (_syncLock)
            {
                if (!_userToLobbyCode.TryGetValue(userId, out string? lobbyCode)
                    || !_roomsByCode.TryGetValue(lobbyCode, out ActiveLobbyRoom? room))
                {
                    return null;
                }

                room.Subscribers[userId] = responseStream;
                return BuildLobbyDetails(room);
            }
        }

        public void UnregisterSubscriber(int userId)
        {
            LeaveLobby(userId);
        }

        private void RemovePlayerFromCurrentLobbyInternal(int userId)
        {
            if (_userToLobbyCode.TryGetValue(userId, out string? currentCode)
                && _roomsByCode.TryGetValue(currentCode, out ActiveLobbyRoom? currentRoom))
            {
                RemovePlayerFromRoom(currentRoom, userId);
                _userToLobbyCode.Remove(userId);
            }
        }

        private static void RemovePlayerFromRoom(ActiveLobbyRoom room, int userId)
        {
            room.Subscribers.Remove(userId);
            LobbyPlayer? target = room.Players.FirstOrDefault(candidate => candidate.Player.UserId == userId);
            if (target != null)
            {
                room.Players.Remove(target);
            }

            if (room.Players.Count > 0 && !room.Players.Any(candidate => candidate.IsHost))
            {
                room.Players[0].IsHost = true;
                room.HostUserId = room.Players[0].Player.UserId;
            }
        }

        private static ActiveLobbyRoom CreateRoomFromEntity(RiskGame.Models.Lobby dbLobby, int initialHostUserId)
        {
            RulesVariant variant = string.Equals(dbLobby.RulesVariant, "Fast", StringComparison.OrdinalIgnoreCase)
                ? RulesVariant.Fast
                : RulesVariant.Standard;

            return new ActiveLobbyRoom
            {
                LobbyId = dbLobby.IdLobby,
                LobbyCode = dbLobby.LobbyCode,
                HostUserId = initialHostUserId,
                Rules = new LobbyRules
                {
                    RulesVariant = variant,
                    TurnDurationSeconds = dbLobby.TurnDuration,
                    MaxPlayers = MaxPlayersPerLobby,
                    IsPrivate = false
                }
            };
        }

        private static PlayerColor AssignAvailableColor(IReadOnlyList<LobbyPlayer> existingPlayers)
        {
            PlayerColor[] allColors = new[]
            {
                PlayerColor.Red,
                PlayerColor.Blue,
                PlayerColor.Green,
                PlayerColor.Yellow
            };

            foreach (PlayerColor color in allColors)
            {
                if (!existingPlayers.Any(player => player.Color == color))
                {
                    return color;
                }
            }

            return PlayerColor.Red;
        }

        private static LobbyDetails BuildLobbyDetails(ActiveLobbyRoom room)
        {
            LobbyDetails details = new LobbyDetails
            {
                LobbyId = room.LobbyId,
                LobbyCode = room.LobbyCode,
                HostUserId = room.HostUserId,
                Rules = room.Rules.Clone(),
                Status = LobbyStatus.Waiting
            };

            foreach (LobbyPlayer player in room.Players)
            {
                details.Players.Add(player.Clone());
            }

            return details;
        }

        private static void BroadcastLobbyChanged(
            IReadOnlyList<IServerStreamWriter<LobbyEvent>> subscribers,
            LobbyChangeType changeType,
            LobbyDetails lobbyDetails,
            int affectedUserId)
        {
            LobbyEvent notification = new LobbyEvent
            {
                LobbyChanged = new LobbyChanged
                {
                    Change = changeType,
                    Lobby = lobbyDetails,
                    AffectedUserId = affectedUserId
                }
            };

            foreach (IServerStreamWriter<LobbyEvent> subscriber in subscribers)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await subscriber.WriteAsync(notification);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (RpcException)
                    {
                    }
                });
            }
        }

        private sealed class ActiveLobbyRoom
        {
            public int LobbyId { get; init; }

            public string LobbyCode { get; init; } = string.Empty;

            public int HostUserId { get; set; }

            public LobbyRules Rules { get; init; } = new LobbyRules();

            public List<LobbyPlayer> Players { get; } = new List<LobbyPlayer>();

            public Dictionary<int, IServerStreamWriter<LobbyEvent>> Subscribers { get; } = new Dictionary<int, IServerStreamWriter<LobbyEvent>>();
        }
    }
}
