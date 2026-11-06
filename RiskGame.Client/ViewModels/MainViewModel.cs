using CommunityToolkit.Mvvm.ComponentModel;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGuest))]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _loggedUsername = string.Empty;

        [ObservableProperty]
        private string? _avatarPath;

        public MainViewModel()
        {
            RefreshFromSession();
        }

        public bool IsGuest => !IsLoggedIn;

        public void Logout()
        {
            SessionService.Logout();
            RefreshFromSession();
        }

        private void RefreshFromSession()
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser != null)
            {
                IsLoggedIn = true;
                LoggedUsername = currentSessionUser.Username;
                AvatarPath = currentSessionUser.AvatarReference;
            }
            else
            {
                IsLoggedIn = false;
                LoggedUsername = string.Empty;
                AvatarPath = null;
            }
        }
    }
}