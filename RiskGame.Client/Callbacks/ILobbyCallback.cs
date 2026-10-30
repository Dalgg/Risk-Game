using RiskGame.Contracts.Lobby;

namespace RiskGame.Callbacks
{
    public interface ILobbyCallback
    {
        void LobbyChanged(LobbyChanged notification);
        void LobbyClosed(LobbyClosed notification);
        void MatchStarting(MatchStarting notification);
    }
}
