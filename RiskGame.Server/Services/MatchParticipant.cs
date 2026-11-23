using System;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class MatchParticipant
    {
        private const int GracePeriodSeconds = 30;
        private const int NoRemainingSeconds = 0;
        private readonly TimeProvider timeProvider;
        private long disconnectedTimestamp;

        public MatchParticipant(LobbyPlayer lobbyPlayer) : this(lobbyPlayer, TimeProvider.System)
        {
        }

        public MatchParticipant(LobbyPlayer lobbyPlayer, TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
            disconnectedTimestamp = timeProvider.GetTimestamp();
            UserId = lobbyPlayer.Player.UserId;
            Nickname = lobbyPlayer.Player.Nickname;
            AvatarReference = lobbyPlayer.Player.AvatarReference;
            Color = lobbyPlayer.Color;
            IsGuest = lobbyPlayer.Player.IsGuest;
        }

        public int UserId { get; }

        public string Nickname { get; }

        public string AvatarReference { get; }

        public PlayerColor Color { get; }

        public bool IsGuest { get; }

        public bool IsConnected { get; private set; }

        public bool HasAbandoned { get; private set; }

        public static int ReconnectionGraceSeconds => GracePeriodSeconds;

        public int ReconnectionSecondsRemaining
        {
            get
            {
                TimeSpan remaining = TimeSpan.Zero;
                if (!IsConnected && !HasAbandoned)
                {
                    remaining = TimeSpan.FromSeconds(ReconnectionGraceSeconds)
                        - timeProvider.GetElapsedTime(disconnectedTimestamp);
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }
                }
                return (int)Math.Ceiling(remaining.TotalSeconds);
            }
        }

        public void SetConnected(bool isConnected)
        {
            if (HasAbandoned && isConnected)
            {
                throw new MatchActionException("The participant has already abandoned the match.");
            }
            if (IsConnected && !isConnected)
            {
                disconnectedTimestamp = timeProvider.GetTimestamp();
            }
            IsConnected = isConnected;
        }

        public bool RefreshConnection()
        {
            bool changed = false;
            if (!IsConnected && !HasAbandoned && ReconnectionSecondsRemaining == NoRemainingSeconds)
            {
                Abandon();
                changed = true;
            }
            return changed;
        }

        public void Abandon()
        {
            HasAbandoned = true;
            IsConnected = false;
        }
    }
}
