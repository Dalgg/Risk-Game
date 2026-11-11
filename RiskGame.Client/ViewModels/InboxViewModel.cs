using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Friend;
using RiskGame.Contracts.Invitation;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class InboxViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 10;
        private const int EmptyRequestCount = 0;
        private const int InvitationTabIndex = 1;
        private readonly SessionUser currentSessionUser;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanNavigate))]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private bool isBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private PendingFriendRequest? selectedRequest;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private PendingInvitation? selectedInvitation;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private int selectedTabIndex;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public InboxViewModel(SessionUser currentSessionUser)
        {
            this.currentSessionUser = currentSessionUser;
        }

        public ObservableCollection<PendingFriendRequest> Requests { get; } =
            new ObservableCollection<PendingFriendRequest>();

        public ObservableCollection<PendingInvitation> Invitations { get; } =
            new ObservableCollection<PendingInvitation>();

        public event EventHandler<LobbyJoinedEventArgs>? LobbyJoined;

        public bool CanNavigate => !IsBusy;

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
                PendingInvitationList pendingInvitations = QueryInvitations();
                Dispatcher.UIThread.Post(() => ApplyInbox(pendingRequests, pendingInvitations));
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

        private PendingInvitationList QueryInvitations()
        {
            InvitationInboxRequest inboxRequest = new InvitationInboxRequest
            {
                UserId = currentSessionUser.IdUser,
                SessionToken = currentSessionUser.SessionToken
            };
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Invitation.InvitationClient invitationClient = new Invitation.InvitationClient(grpcChannel);
            return invitationClient.GetPendingInvitations(inboxRequest,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private void ApplyInbox(PendingFriendRequestList pendingRequests, PendingInvitationList pendingInvitations)
        {
            ApplyRequests(pendingRequests);
            Invitations.Clear();
            SelectedInvitation = null;
            foreach (PendingInvitation pendingInvitation in pendingInvitations.Invitations)
            {
                Invitations.Add(pendingInvitation);
            }
            StatusMessage = Strings.InboxSelectRequest;
            if (Requests.Count == EmptyRequestCount && Invitations.Count == EmptyRequestCount)
            {
                StatusMessage = Strings.InboxEmpty;
            }
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
            RespondToSelection(true);
        }

        [RelayCommand(CanExecute = nameof(CanRespond))]
        private void Reject()
        {
            RespondToSelection(false);
        }

        private void RespondToSelection(bool accept)
        {
            if (SelectedTabIndex == InvitationTabIndex)
            {
                RespondToInvitation(accept);
            }
            else
            {
                RespondToSelectedRequest(accept);
            }
        }

        private void RespondToInvitation(bool accept)
        {
            PendingInvitation? pendingInvitation = SelectedInvitation;
            if (pendingInvitation != null)
            {
                InvitationAction invitationAction = new InvitationAction
                {
                    UserId = currentSessionUser.IdUser,
                    SessionToken = currentSessionUser.SessionToken,
                    InvitationId = pendingInvitation.InvitationId
                };
                IsBusy = true;
                StatusMessage = Strings.InboxSaving;
                Task.Run(() => SaveInvitationReply(invitationAction, accept));
            }
        }

        private void SaveInvitationReply(InvitationAction invitationAction, bool accept)
        {
            try
            {
                if (accept)
                {
                    LobbyDetails lobbyDetails = AcceptLobbyInvitation(invitationAction);
                    Dispatcher.UIThread.Post(() => NotifyLobbyJoined(lobbyDetails));
                }
                else
                {
                    RejectLobbyInvitation(invitationAction);
                    Dispatcher.UIThread.Post(Refresh);
                }
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not respond to the lobby invitation.");
                string errorMessage = Strings.InboxConnectionError;
                if (rpcException.StatusCode == StatusCode.FailedPrecondition)
                {
                    errorMessage = Strings.InboxInvitationUnavailable;
                }
                if (rpcException.StatusCode == StatusCode.Unauthenticated)
                {
                    errorMessage = Strings.FriendsLoginRequired;
                }
                Dispatcher.UIThread.Post(() => ShowError(errorMessage));
            }
        }

        private LobbyDetails AcceptLobbyInvitation(InvitationAction invitationAction)
        {
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Invitation.InvitationClient invitationClient = new Invitation.InvitationClient(grpcChannel);
            return invitationClient.AcceptInvitation(invitationAction,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private Empty RejectLobbyInvitation(InvitationAction invitationAction)
        {
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Invitation.InvitationClient invitationClient = new Invitation.InvitationClient(grpcChannel);
            return invitationClient.RejectInvitation(invitationAction,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private void NotifyLobbyJoined(LobbyDetails lobbyDetails)
        {
            IsBusy = false;
            EventHandler<LobbyJoinedEventArgs>? lobbyJoinedHandler = LobbyJoined;
            if (lobbyJoinedHandler != null)
            {
                lobbyJoinedHandler(this, new LobbyJoinedEventArgs(lobbyDetails));
            }
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
            if (SelectedTabIndex == InvitationTabIndex)
            {
                return !IsBusy && SelectedInvitation != null;
            }
            return !IsBusy && SelectedRequest != null;
        }
    }
}
