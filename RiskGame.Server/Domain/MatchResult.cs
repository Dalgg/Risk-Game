using System;
using System.Collections.Generic;

namespace RiskGame.Models
{
    public partial class MatchResult
    {
        public int IdMatchResult { get; set; }

        public int IdMatch { get; set; }

        public int IdUser { get; set; }

        public int? FinalScore { get; set; }

        public int? FinalPosition { get; set; }

        public virtual Match IdMatchNavigation { get; set; } = null!;

        public virtual User IdUserNavigation { get; set; } = null!;
    }

}

