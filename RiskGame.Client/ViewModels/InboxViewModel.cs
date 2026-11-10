using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Friend;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class InboxViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 10;
        private const int EmptyRequestCount = 0;
        private readonly SessionUser currentSessionUser;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private bool isBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private PendingFriendRequest? selectedRequest;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public InboxViewModel(SessionUser currentSessionUser)
        {
            this.currentSessionUser = currentSessionUser;
        }

        public ObservableCollection<PendingFriendRequest> Requests { get; } =
            new ObservableCollection<PendingFriendRequest>();

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private void Refresh()
        {
            IsBusy = true;
            StatusMessage = Strings.InboxLoading;
            Task.Run(LoadRequests);
        }

        private void LoadRequests()
        {
            try
            {
                PendingFriendRequestList pendingRequests = QueryRequests();
                Dispatcher.UIThread.Post(() => ApplyRequests(pendingRequests));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not load the friendship inbox.");
                DataOperationException inboxException = CreateInboxException(rpcException);
                Dispatcher.UIThread.Post(() => ShowError(inboxException.Message));
            }
        }

        private PendingFriendRequestList QueryRequests()
        {
            FriendInboxRequest inboxRequest = new FriendInboxRequest
            {
                UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken
            };
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Friend.FriendClient friendClient = new Friend.FriendClient(grpcChannel);
            return friendClient.GetPendingRequests(inboxRequest,
                deadline: System.DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private void ApplyRequests(PendingFriendRequestList pendingRequests)
        {
            Requests.Clear();
            SelectedRequest = null;
            foreach (PendingFriendRequest pendingRequest in pendingRequests.Requests)
            {
                Requests.Add(pendingRequest);
            }
            StatusMessage = Strings.InboxSelectRequest;
            if (Requests.Count == EmptyRequestCount)
            {
                StatusMessage = Strings.InboxEmpty;
            }
            IsBusy = false;
        }

        [RelayCommand(CanExecute = nameof(CanRespond))]
        private void Accept()
        {
            RespondToSelectedRequest(true);
        }

        [RelayCommand(CanExecute = nameof(CanRespond))]
        private void Reject()
        {
            RespondToSelectedRequest(false);
        }

        private void RespondToSelectedRequest(bool accept)
        {
            PendingFriendRequest? pendingRequest = SelectedRequest;
            if (pendingRequest != null)
            {
                FriendRequestReply requestReply = new FriendRequestReply
                {
                    UserId = currentSessionUser.IdUser,
                    SessionToken = currentSessionUser.SessionToken,
                    RequestId = pendingRequest.RequestId,
                    Accept = accept
                };
                IsBusy = true;
                StatusMessage = Strings.InboxSaving;
                Task.Run(() => SaveReply(requestReply));
            }
        }

        private void SaveReply(FriendRequestReply requestReply)
        {
            try
            {
                SubmitReply(requestReply);
                Dispatcher.UIThread.Post(Refresh);
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not respond to the selected friendship request.");
                DataOperationException inboxException = CreateInboxException(rpcException);
                Dispatcher.UIThread.Post(() => ShowError(inboxException.Message));
            }
        }

        private Empty SubmitReply(FriendRequestReply requestReply)
        {
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Friend.FriendClient friendClient = new Friend.FriendClient(grpcChannel);
            return friendClient.RespondToRequest(requestReply,
                deadline: System.DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private DataOperationException CreateInboxException(RpcException rpcException)
        {
            string errorMessage = Strings.InboxConnectionError;
            if (rpcException.StatusCode == StatusCode.Unauthenticated)
            {
                errorMessage = Strings.FriendsLoginRequired;
            }
            if (rpcException.StatusCode == StatusCode.FailedPrecondition)
            {
                errorMessage = Strings.InboxRequestUnavailable;
            }
            return new DataOperationException(errorMessage, rpcException);
        }

        private void ShowError(string errorMessage)
        {
            StatusMessage = errorMessage;
            IsBusy = false;
        }

        private bool CanRefresh()
        {
            return !IsBusy;
        }

        private bool CanRespond()
        {
            return !IsBusy && SelectedRequest != null;
        }
    }
}
