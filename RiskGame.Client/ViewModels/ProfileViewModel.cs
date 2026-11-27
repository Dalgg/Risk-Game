using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Profile;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class ProfileViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 10;
        private const int FirstAvatarIndex = 0;
        private const int MinimumNicknameLength = 1;
        private const int MaximumNicknameLength = 50;
        private const int NoRecentMatches = 0;
        private readonly SessionUser currentSessionUser;

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        public partial string Nickname { get; set; }

        [ObservableProperty]
        public partial string EditedNickname { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvatarImage))]
        public partial string AvatarReference { get; set; }

        [ObservableProperty]
        private string matchesPlayed = Strings.ProfileStatisticUnavailable;

        [ObservableProperty]
        private string matchesWon = Strings.ProfileStatisticUnavailable;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool showEmptyHistory;

        [ObservableProperty]
        public partial bool IsEditingNickname { get; set; }

        [ObservableProperty]
        public partial bool IsChoosingAvatar { get; set; }

        [ObservableProperty]
        public partial bool IsEditingPassword { get; set; }

        [ObservableProperty]
        public partial string CurrentPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string NewPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConfirmNewPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ProfileAvatarOption? SelectedAvatar { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
        [NotifyCanExecuteChangedFor(nameof(BackCommand))]
        [NotifyCanExecuteChangedFor(nameof(LogoutCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditNicknameCommand))]
        [NotifyCanExecuteChangedFor(nameof(ChooseAvatarCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditPasswordCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveNicknameCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveAvatarCommand))]
        [NotifyCanExecuteChangedFor(nameof(SavePasswordCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
        private bool isLoading;

        public ProfileViewModel(SessionUser session)
        {
            currentSessionUser = session;
            username = session.Username;
            Nickname = session.Nickname;
            EditedNickname = session.Nickname;
            AvatarReference = string.Empty;
            if (session.AvatarReference != null)
            {
                AvatarReference = session.AvatarReference;
            }
            Avatars = ProfileAvatarCatalog.CreateOptions();
            SelectedAvatar = Avatars[FirstAvatarIndex];
        }

        public string UserId => currentSessionUser.IdUser.ToString(CultureInfo.CurrentCulture);

        public static int NicknameMaximumLength => MaximumNicknameLength;

        public IImage AvatarImage => ProfileAvatarCatalog.GetImage(AvatarReference);

        public IReadOnlyList<ProfileAvatarOption> Avatars { get; }

        public ObservableCollection<ProfileMatchHistoryItem> RecentMatches { get; } =
            new ObservableCollection<ProfileMatchHistoryItem>();

        private bool CanRetry()
        {
            return !IsLoading;
        }

        private bool CanEdit()
        {
            return !IsLoading && !currentSessionUser.IsGuest;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void EditNickname()
        {
            EditedNickname = Nickname;
            IsChoosingAvatar = false;
            IsEditingPassword = false;
            IsEditingNickname = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void ChooseAvatar()
        {
            SelectedAvatar = Avatars.FirstOrDefault(option => option.Reference == AvatarReference);
            if (SelectedAvatar == null)
            {
                SelectedAvatar = Avatars[FirstAvatarIndex];
            }
            IsEditingNickname = false;
            IsEditingPassword = false;
            IsChoosingAvatar = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void EditPassword()
        {
            ClearPasswordInputs();
            IsEditingNickname = false;
            IsChoosingAvatar = false;
            IsEditingPassword = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void CancelEdit()
        {
            IsEditingNickname = false;
            IsChoosingAvatar = false;
            IsEditingPassword = false;
            ClearPasswordInputs();
            StatusMessage = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void SavePassword()
        {
            string validationError = ValidatePasswordChange();
            if (!string.IsNullOrEmpty(validationError))
            {
                StatusMessage = validationError;
            }
            else
            {
                string currentPasswordValue = CurrentPassword;
                string newPasswordValue = NewPassword;
                IsLoading = true;
                Task.Run(() => SubmitPasswordChange(currentPasswordValue, newPasswordValue));
            }
        }

        private string ValidatePasswordChange()
        {
            bool hasEmptyField = string.IsNullOrWhiteSpace(CurrentPassword)
                || string.IsNullOrWhiteSpace(NewPassword);
            if (hasEmptyField || string.IsNullOrWhiteSpace(ConfirmNewPassword))
            {
                return Strings.ProfilePasswordEmptyError;
            }
            if (NewPassword != ConfirmNewPassword)
            {
                return Strings.ProfilePasswordMismatchError;
            }
            if (!PasswordPolicy.PasswordIsValid(NewPassword))
            {
                return Strings.ProfilePasswordPolicyError;
            }
            if (CurrentPassword == NewPassword)
            {
                return Strings.ProfilePasswordSameError;
            }
            return string.Empty;
        }

        private void SubmitPasswordChange(string currentPasswordValue, string newPasswordValue)
        {
            try
            {
                RequestPasswordUpdate(currentPasswordValue, newPasswordValue);
                Dispatcher.UIThread.Post(CompletePasswordChange);
            }
            catch (RpcException rpcException)
            {
                HandlePasswordRpcFailure(rpcException);
            }
        }

        private void RequestPasswordUpdate(string currentPasswordValue, string newPasswordValue)
        {
            UpdatePasswordRequest request = new UpdatePasswordRequest
            {
                UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken,
                CurrentPassword = currentPasswordValue,
                NewPassword = newPasswordValue
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Profile.ProfileClient client = new Profile.ProfileClient(channel);
            client.UpdatePassword(
                request,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: CancellationToken.None);
        }

        private void CompletePasswordChange()
        {
            ClearPasswordInputs();
            IsEditingPassword = false;
            IsLoading = false;
            StatusMessage = Strings.ProfilePasswordSaved;
        }

        private void HandlePasswordRpcFailure(RpcException rpcException)
        {
            if (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                Dispatcher.UIThread.Post(() => ShowError(Strings.ProfilePasswordCurrentWrongError));
            }
            else if (rpcException.StatusCode == StatusCode.InvalidArgument)
            {
                Dispatcher.UIThread.Post(() => ShowError(Strings.ProfilePasswordPolicyError));
            }
            else
            {
                HandleRpcFailure(rpcException, Strings.ProfileSaveError);
            }
        }

        private void ClearPasswordInputs()
        {
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void SaveNickname()
        {
            string normalized = string.Empty;
            if (EditedNickname != null)
            {
                normalized = EditedNickname.Trim();
            }
            bool invalidLength = normalized.Length < MinimumNicknameLength || normalized.Length > MaximumNicknameLength;
            if (invalidLength || normalized.Any(char.IsControl))
            {
                StatusMessage = Strings.ProfileNicknameInvalid;
            }
            else
            {
                IsLoading = true;
                Task.Run(() => SubmitChange(() => RequestNicknameUpdate(normalized)));
            }
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void SaveAvatar()
        {
            ProfileAvatarOption? avatar = SelectedAvatar;
            if (avatar != null)
            {
                IsLoading = true;
                Task.Run(() => SubmitChange(() => RequestAvatarUpdate(avatar.Reference)));
            }
        }

        private void SubmitChange(Func<UserProfile> operation)
        {
            try
            {
                UserProfile profile = operation();
                Dispatcher.UIThread.Post(() => CompleteChange(profile));
            }
            catch (RpcException rpcException)
            {
                HandleRpcFailure(rpcException, Strings.ProfileSaveError);
            }
        }

        private void CompleteChange(UserProfile profile)
        {
            if (ApplyProfile(profile))
            {
                IsEditingNickname = false;
                IsChoosingAvatar = false;
                StatusMessage = Strings.ProfileSaved;
            }
        }

        private void LoadProfile()
        {
            try
            {
                UserProfile profile = RequestProfile();
                Dispatcher.UIThread.Post(() => ApplyProfile(profile));
            }
            catch (RpcException rpcException)
            {
                HandleRpcFailure(rpcException, Strings.ProfileLoadError);
            }
        }

        private void HandleRpcFailure(RpcException exception, string message)
        {
            bool expectedFailure = exception.StatusCode == StatusCode.InvalidArgument
                || exception.StatusCode == StatusCode.Unauthenticated;
            if (!expectedFailure && exception.StatusCode != StatusCode.PermissionDenied)
            {
                JsonLogger.Error(exception, "Could not complete the profile operation.");
            }
            Dispatcher.UIThread.Post(() => ShowError(message));
        }

        private UserProfile RequestProfile()
        {
            ProfileQuery request = new ProfileQuery
            {
                UserId = currentSessionUser.IdUser, SessionToken = currentSessionUser.SessionToken
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Profile.ProfileClient client = new Profile.ProfileClient(channel);
            return client.GetProfile(
                request,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: CancellationToken.None);
        }

        private UserProfile RequestNicknameUpdate(string nickname)
        {
            UpdateNicknameRequest request = new UpdateNicknameRequest
            {
                UserId = currentSessionUser.IdUser, SessionToken = currentSessionUser.SessionToken, Nickname = nickname
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Profile.ProfileClient client = new Profile.ProfileClient(channel);
            return client.UpdateNickname(
                request,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: CancellationToken.None);
        }

        private UserProfile RequestAvatarUpdate(string reference)
        {
            UpdateAvatarRequest request = new UpdateAvatarRequest
            {
                UserId = currentSessionUser.IdUser, SessionToken = currentSessionUser.SessionToken,
                AvatarReference = reference
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            Profile.ProfileClient client = new Profile.ProfileClient(channel);
            return client.UpdateAvatar(
                request,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
                cancellationToken: CancellationToken.None);
        }

        private bool ApplyProfile(UserProfile profile)
        {
            bool updated = SessionService.UpdateProfile(currentSessionUser, profile);
            if (updated)
            {
                Username = profile.Username;
                Nickname = profile.Nickname;
                EditedNickname = profile.Nickname;
                AvatarReference = profile.AvatarReference;
                MatchesPlayed = profile.MatchesPlayed.ToString(CultureInfo.CurrentCulture);
                MatchesWon = profile.MatchesWon.ToString(CultureInfo.CurrentCulture);
                ApplyRecentMatches(profile);
                StatusMessage = string.Empty;
            }
            else
            {
                StatusMessage = Strings.ProfileLoadError;
            }
            IsLoading = false;
            return updated;
        }

        private void ApplyRecentMatches(UserProfile profile)
        {
            RecentMatches.Clear();
            foreach (RecentMatchResult result in profile.RecentMatches)
            {
                RecentMatches.Add(new ProfileMatchHistoryItem(result));
            }
            ShowEmptyHistory = RecentMatches.Count == NoRecentMatches;
        }

        private void ShowError(string message)
        {
            StatusMessage = message;
            IsLoading = false;
        }

        [RelayCommand(CanExecute = nameof(CanRetry))]
        private void Back()
        {
            ProfileNavigationService navigation = new ProfileNavigationService();
            navigation.ReturnToMenu();
        }

        [RelayCommand(CanExecute = nameof(CanRetry))]
        private void Logout()
        {
            SessionService.Logout();
            ProfileNavigationService navigation = new ProfileNavigationService();
            navigation.ReturnToMenu();
        }

        [RelayCommand(CanExecute = nameof(CanRetry))]
        private void Retry()
        {
            IsLoading = true;
            Task.Run(LoadProfile);
        }
    }
}
