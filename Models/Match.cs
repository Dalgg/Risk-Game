using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class Match
    {
        public int IdMatch { get; set; }

        public int IdLobby { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? EndReason { get; set; }

        public virtual Lobby IdLobbyNavigation { get; set; } = null!;

        public virtual ICollection<MatchResult> MatchResults { get; set; } = new List<MatchResult>();

        public virtual MatchState? MatchState { get; set; }
    }

}
