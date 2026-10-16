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
            SessionUser? user = SessionService.CurrentUser;
            IsLoggedIn = user != null;
            LoggedUsername = user?.Username ?? string.Empty;
            AvatarPath = user?.AvatarReference;
        }
    }
}