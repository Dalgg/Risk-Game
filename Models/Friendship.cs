using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class Friendship
    {
        public int IdFriendship { get; set; }

        public int IdUser1 { get; set; }

        public int IdUser2 { get; set; }

        public string Status { get; set; } = null!;

        public DateTime RequestDate { get; set; }

        public DateTime? AcceptanceDate { get; set; }

        public virtual User IdUser1Navigation { get; set; } = null!;

        public virtual User IdUser2Navigation { get; set; } = null!;
    }

}
