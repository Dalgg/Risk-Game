using System;
using System.Collections.Generic;

namespace RiskGame.Models
{
    public partial class Lobby
    {
        public int IdLobby { get; set; }

        public string LobbyCode { get; set; } = null!;

        public string RulesVariant { get; set; } = null!;

        public int TurnDuration { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime? ClosureDate { get; set; }

        public virtual ICollection<LobbyInvitation> LobbyInvitations { get; set; } = new List<LobbyInvitation>();

        public virtual ICollection<Match> Matches { get; set; } = new List<Match>();
    }

}

