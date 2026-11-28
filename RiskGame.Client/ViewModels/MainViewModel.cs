using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly InboxNotificationClient inboxNotificationClient;
        private CancellationTokenSource? inboxRefreshCancellation;
        private bool isRefreshingInbox;
        private bool inboxRefreshRequested;
        private bool notificationsStopped;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGuest))]
        private bool isLoggedIn;

        [ObservableProperty]
        public partial string LoggedNickname { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvatarImage))]
        private string? avatarPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InboxButtonDescription))]
        private bool hasPendingInboxNotifications;

        public MainViewModel() : this(new InboxNotificationClient())
        {
        }

        public MainViewModel(InboxNotificationClient inboxNotificationClient)
        {
            this.inboxNotificationClient = inboxNotificationClient;
            LoggedNickname = string.Empty;
            RefreshFromSession();
        }

        public bool IsGuest => !IsLoggedIn;

        public IImage AvatarImage => ProfileAvatarCatalog.GetImage(AvatarPath);

        public string InboxButtonDescription
        {
            get
            {
                string description = Strings.InboxTitle;
                if (HasPendingInboxNotifications)
                {
                    description = Strings.InboxPendingNotifications;
                }
                return description;
            }
        }

        public void RefreshInboxNotifications()
        {
            if (!notificationsStopped)
            {
                SessionUser? session = SessionService.CurrentUser;
                if (session != null && !session.IsGuest)
                {
                    QueueInboxRefresh(session);
                }
                else
                {
                    HasPendingInboxNotifications = false;
                    inboxRefreshRequested = false;
                    CancelInboxRefresh();
                }
            }
        }

        public void StopInboxNotifications()
        {
            notificationsStopped = true;
            inboxRefreshRequested = false;
            CancelInboxRefresh();
        }

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
                HasPendingInboxNotifications = false;
                LoggedNickname = string.Empty;
                AvatarPath = null;
            }
        }

        private void QueueInboxRefresh(SessionUser session)
        {
            if (isRefreshingInbox)
            {
                inboxRefreshRequested = true;
            }
            else
            {
                inboxRefreshRequested = false;
                isRefreshingInbox = true;
                inboxRefreshCancellation = new CancellationTokenSource();
                CancellationToken cancellationToken = inboxRefreshCancellation.Token;
                Task.Run(() => LoadInboxNotifications(session, cancellationToken));
            }
        }

        private void LoadInboxNotifications(SessionUser session, CancellationToken cancellationToken)
        {
            bool loaded = false;
            bool hasPending = false;
            try
            {
                hasPending = inboxNotificationClient.HasPending(session, cancellationToken);
                loaded = true;
            }
            catch (RpcException rpcException)
            {
                LogInboxFailure(rpcException, session, cancellationToken);
            }
            catch (OperationCanceledException canceledException)
            {
                LogInboxFailure(canceledException, session, cancellationToken);
            }
            Dispatcher.UIThread.Post(() => ApplyInboxStatus(session, loaded, hasPending));
        }

        private static void LogInboxFailure(Exception exception, SessionUser session, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                JsonLogger.Error(exception, "Could not refresh the inbox notification indicator for user {0}.",
                    session.IdUser);
            }
        }

        private void ApplyInboxStatus(SessionUser expectedSession, bool loaded, bool hasPending)
        {
            CompleteInboxRefresh();
            isRefreshingInbox = false;
            if (!notificationsStopped)
            {
                if (!IsCurrentInboxSession(expectedSession))
                {
                    HasPendingInboxNotifications = false;
                }
                else if (loaded)
                {
                    HasPendingInboxNotifications = hasPending;
                }
                if (inboxRefreshRequested)
                {
                    inboxRefreshRequested = false;
                    RefreshInboxNotifications();
                }
            }
        }

        private static bool IsCurrentInboxSession(SessionUser expectedSession)
        {
            bool isCurrentSession = false;
            SessionUser? session = SessionService.CurrentUser;
            if (session != null)
            {
                bool sameAccount = session.IdUser == expectedSession.IdUser
                    && session.SessionToken == expectedSession.SessionToken;
                isCurrentSession = sameAccount && !session.IsGuest;
            }
            return isCurrentSession;
        }

        private void CancelInboxRefresh()
        {
            if (inboxRefreshCancellation != null)
            {
                inboxRefreshCancellation.Cancel();
            }
        }

        private void CompleteInboxRefresh()
        {
            if (inboxRefreshCancellation != null)
            {
                inboxRefreshCancellation.Dispose();
                inboxRefreshCancellation = null;
            }
        }
    }
}
