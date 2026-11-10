using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGuest))]
        private bool isLoggedIn;

        [ObservableProperty]
        private string loggedUsername = string.Empty;

        [ObservableProperty]
        private string? avatarPath;

        public MainViewModel()
        {
            RefreshFromSession();
        }

        public bool IsGuest => !IsLoggedIn;

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
