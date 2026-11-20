using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiskGame.Services;
using Avalonia.Media;

namespace RiskGame.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGuest))]
        private bool isLoggedIn;

        [ObservableProperty]
        private string loggedNickname = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvatarImage))]
        private string? avatarPath;

        public MainViewModel()
        {
            RefreshFromSession();
        }

        public bool IsGuest => !IsLoggedIn;

        public IImage AvatarImage => ProfileAvatarCatalog.GetImage(AvatarPath);

        [RelayCommand]
        private void OpenProfile()
        {
            ProfileNavigationService navigation = new ProfileNavigationService();
            navigation.OpenProfile();
        }

        public void Logout()
        {
            SessionService.Logout();
            RefreshFromSession();
        }

        private void RefreshFromSession()
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser != null && !currentSessionUser.IsGuest)
            {
                IsLoggedIn = true;
                LoggedNickname = currentSessionUser.Nickname;
                AvatarPath = currentSessionUser.AvatarReference;
            }
            else
            {
                IsLoggedIn = false;
                LoggedNickname = string.Empty;
                AvatarPath = null;
            }
        }
    }
}
