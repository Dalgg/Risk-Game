using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Profile;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class ProfileViewModel : ViewModelBase
    {
        private const string DefaultAvatarPath = "/Assets/default_avatar.png";
        private const int RequestTimeoutSeconds = 10;
        private readonly SessionUser currentSessionUser;

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        private string matchesPlayed = Strings.ProfileStatisticUnavailable;

        [ObservableProperty]
        private string matchesWon = Strings.ProfileStatisticUnavailable;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
        private bool isLoading;

        public ProfileViewModel(SessionUser session)
        {
            currentSessionUser = session;
            username = session.Username;
        }

        public string UserId => currentSessionUser.IdUser.ToString(CultureInfo.CurrentCulture);

        public string AvatarPath => DefaultAvatarPath;

        private void LoadProfile()
        {
            try
            {
                UserProfile profile = RequestProfile();
                Dispatcher.UIThread.Post(() => ApplyProfile(profile));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Failed to load the current player's profile.");
                Dispatcher.UIThread.Post(ShowLoadError);
            }
        }

        private UserProfile RequestProfile()
        {
            ProfileQuery profileQuery = new ProfileQuery
            {
                UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Profile.ProfileClient profileClient = new Profile.ProfileClient(grpcChannel);
                return profileClient.GetProfile(profileQuery,
                    deadline: System.DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Communication failed while retrieving the player profile.");
                throw new DataOperationException(Strings.ProfileLoadError, rpcException);
            }
        }

        private void ApplyProfile(UserProfile profile)
        {
            Username = profile.Username;
            MatchesPlayed = profile.MatchesPlayed.ToString(CultureInfo.CurrentCulture);
            MatchesWon = profile.MatchesWon.ToString(CultureInfo.CurrentCulture);
            StatusMessage = string.Empty;
            IsLoading = false;
        }

        private void ShowLoadError()
        {
            StatusMessage = Strings.ProfileLoadError;
            IsLoading = false;
        }

        [RelayCommand]
        private void Back()
        {
            ProfileNavigationService navigation = new ProfileNavigationService();
            navigation.ReturnToMenu();
        }

        [RelayCommand(CanExecute = nameof(CanRetry))]
        private void Retry()
        {
            IsLoading = true;
            Task.Run(LoadProfile);
        }

        private bool CanRetry()
        {
            return !IsLoading;
        }
    }
}

