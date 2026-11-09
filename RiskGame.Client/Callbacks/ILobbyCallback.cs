using RiskGame.Contracts.Lobby;

namespace RiskGame.Callbacks
{
    public interface ILobbyCallback
    {
        void LobbyUpdated(LobbyDetails lobbyDetails);

        void MatchStarting(int matchId);
    }
}
