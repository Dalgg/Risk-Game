using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;

namespace RiskGame.Server.Services
{
    public sealed class MatchParticipant
    {
        public MatchParticipant(LobbyPlayer lobbyPlayer)
        {
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

        public void SetConnected(bool isConnected)
        {
            IsConnected = isConnected;
        }
    }
}
