using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class MatchState
    {
        public int IdMatch { get; set; }

        public string StateData { get; set; } = null!;

        public DateTime LastUpdated { get; set; }

        public virtual Match IdMatchNavigation { get; set; } = null!;
    }

}
