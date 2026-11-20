using RiskGame.Contracts.Lobby;
using RiskGame.Resources;
using RiskGame.Services;
using Avalonia.Media;

namespace RiskGame.ViewModels
{
    public sealed class FriendListItem
    {
        private readonly string avatarReference;

        public FriendListItem(FriendSummary friend)
        {
            Nickname = friend.Nickname;
            avatarReference = friend.AvatarReference;
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

        public IImage AvatarImage => ProfileAvatarCatalog.GetImage(avatarReference);
    }
}
