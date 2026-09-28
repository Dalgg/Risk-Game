using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class PasswordRecovery
    {
        public int IdRecovery { get; set; }

        public int IdUser { get; set; }

        public string RecoveryCode { get; set; } = null!;

        public DateTime CreationDate { get; set; }

        public DateTime ExpirationDate { get; set; }

        public virtual User IdUserNavigation { get; set; } = null!;
    }

}
