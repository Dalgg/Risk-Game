using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class LobbyInvitation
    {
        public int IdInvitation { get; set; }

        public int IdLobby { get; set; }

        public int IdSender { get; set; }

        public int? IdRecipient { get; set; }

        public string? RecipientEmail { get; set; }

        public string InvitationCode { get; set; } = null!;

        public string Status { get; set; } = null!;

        public DateTime SentDate { get; set; }

        public DateTime ExpirationDate { get; set; }

        public virtual Lobby IdLobbyNavigation { get; set; } = null!;

        public virtual User? IdRecipientNavigation { get; set; }

        public virtual User IdSenderNavigation { get; set; } = null!;
    }

}
