using System.Collections.Generic;
using System;

namespace RiskGame.Models
{
    public partial class User
    {
        public int IdUser { get; set; }

        public string Email { get; set; } = null!;

        public string Username { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string Nickname { get; set; } = null!;

        public string? AvatarReference { get; set; }

        public DateTime RegistrationDate { get; set; }

        public virtual ICollection<Friendship> FriendshipIdUser1Navigations { get; set; } = new List<Friendship>();

        public virtual ICollection<Friendship> FriendshipIdUser2Navigations { get; set; } = new List<Friendship>();

        public virtual ICollection<LobbyInvitation> LobbyInvitationIdRecipientNavigations { get; set; } = new List<LobbyInvitation>();

        public virtual ICollection<LobbyInvitation> LobbyInvitationIdSenderNavigations { get; set; } = new List<LobbyInvitation>();

        public virtual ICollection<MatchResult> MatchResults { get; set; } = new List<MatchResult>();

        public virtual ICollection<PasswordRecovery> PasswordRecoveries { get; set; } = new List<PasswordRecovery>();
    }

}
