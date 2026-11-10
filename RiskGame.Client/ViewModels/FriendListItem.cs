using RiskGame.Contracts.Lobby;
using RiskGame.Resources;

namespace RiskGame.ViewModels
{
    public sealed class FriendListItem
    {
        private const string DefaultAvatarPath = "/Assets/default_avatar.png";

        public FriendListItem(FriendSummary friend)
        {
            Nickname = friend.Nickname;
            IsOnline = friend.IsOnline;
            StatusText = Strings.LobbyFriendOfflineStatus;
            if (friend.IsOnline)
            {
                StatusText = Strings.LobbyFriendOnlineStatus;
            }
        }

        public string Nickname { get; }

        public bool IsOnline { get; }

        public string StatusText { get; }

        public string AvatarPath => DefaultAvatarPath;
    }
}
