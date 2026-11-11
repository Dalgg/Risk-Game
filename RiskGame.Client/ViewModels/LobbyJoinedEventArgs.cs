using System;
using RiskGame.Contracts.Lobby;

namespace RiskGame.ViewModels
{
    public sealed class LobbyJoinedEventArgs : EventArgs
    {
        public LobbyJoinedEventArgs(LobbyDetails lobbyDetails)
        {
            LobbyDetails = lobbyDetails;
        }

        public LobbyDetails LobbyDetails { get; }
    }
}
