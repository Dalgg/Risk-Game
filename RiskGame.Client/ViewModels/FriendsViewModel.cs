using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Friend;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class FriendsViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 10;
        private const int EmptyFriendsCount = 0;
        private readonly SessionUser currentSessionUser;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendRequestCommand))]
        private bool isBusy;

        [ObservableProperty]
        private bool showEmptyMessage;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string targetUsername = string.Empty;

        public FriendsViewModel(SessionUser currentSessionUser)
        {
            this.currentSessionUser = currentSessionUser;
        }

        public ObservableCollection<FriendListItem> Friends { get; } = new ObservableCollection<FriendListItem>();

        public static List<FriendSummary> GetFriends()
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            UserLobbyRequest friendsRequest = new UserLobbyRequest { UserId = currentSessionUser.IdUser };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient lobbyClient = new Lobby.LobbyClient(grpcChannel);
                FriendSummaryList friendSummaryList = lobbyClient.GetFriends(friendsRequest,
                    deadline: System.DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
                return friendSummaryList.Friends.ToList();
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not query the player's friends.");
                throw new DataOperationException(Strings.FriendsLoadError, rpcException);
            }
        }

        [RelayCommand(CanExecute = nameof(CanRequest))]
        private void Refresh()
        {
            IsBusy = true;
            ShowEmptyMessage = false;
            StatusMessage = Strings.FriendsLoading;
            Task.Run(LoadFriends);
        }

        private void LoadFriends()
        {
            try
            {
                List<FriendSummary> friends = GetFriends();
                Dispatcher.UIThread.Post(() => ApplyFriends(friends));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not load friends from the menu.");
                Dispatcher.UIThread.Post(ShowLoadError);
            }
        }

        private void ApplyFriends(List<FriendSummary> friends)
        {
            Friends.Clear();
            foreach (FriendSummary friend in friends)
            {
                Friends.Add(new FriendListItem(friend));
            }
            ShowEmptyMessage = Friends.Count == EmptyFriendsCount;
            StatusMessage = string.Empty;
            IsBusy = false;
        }

        private void ShowLoadError()
        {
            StatusMessage = Strings.FriendsLoadError;
            IsBusy = false;
        }

        [RelayCommand(CanExecute = nameof(CanRequest))]
        private void SendRequest()
        {
            if (string.IsNullOrWhiteSpace(TargetUsername))
            {
                StatusMessage = Strings.FriendsUsernameRequired;
            }
            else
            {
                FriendRequest friendRequest = new FriendRequest
                {
                    UserId = currentSessionUser.IdUser,
                    TargetUsername = TargetUsername.Trim(),
                    SessionToken = currentSessionUser.SessionToken
                };
                IsBusy = true;
                StatusMessage = Strings.FriendsSending;
                Task.Run(() => SendFriendRequest(friendRequest));
            }
        }

        private void SendFriendRequest(FriendRequest friendRequest)
        {
            try
            {
                SubmitFriendRequest(friendRequest);
                Dispatcher.UIThread.Post(ShowRequestSent);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not send a friend request from the menu.");
                Dispatcher.UIThread.Post(() => ShowRequestError(dataOperationException.Message));
            }
        }

        private Empty SubmitFriendRequest(FriendRequest friendRequest)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Friend.FriendClient friendClient = new Friend.FriendClient(grpcChannel);
                return friendClient.SendFriendRequest(friendRequest,
                    deadline: System.DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Friend request failed during gRPC communication.");
                throw CreateRequestException(rpcException);
            }
        }

        private DataOperationException CreateRequestException(RpcException rpcException)
        {
            string errorMessage = Strings.FriendsLoadError;
            if (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                errorMessage = Strings.FriendsRequestRejected;
            }
            if (rpcException.StatusCode == StatusCode.Unauthenticated)
            {
                errorMessage = Strings.FriendsLoginRequired;
            }
            return new DataOperationException(errorMessage, rpcException);
        }

        private void ShowRequestSent()
        {
            StatusMessage = Strings.FriendsRequestSent;
            TargetUsername = string.Empty;
            IsBusy = false;
        }

        private void ShowRequestError(string errorMessage)
        {
            StatusMessage = errorMessage;
            IsBusy = false;
        }

        private bool CanRequest()
        {
            return !IsBusy;
        }
    }
}
