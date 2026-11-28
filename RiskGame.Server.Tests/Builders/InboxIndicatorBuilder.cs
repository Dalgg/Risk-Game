extern alias Client;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Grpc.Core;
using AccountSessionConnection = Client::RiskGame.Services.AccountSessionConnection;
using InboxNotificationClient = Client::RiskGame.Services.InboxNotificationClient;
using MainViewModel = Client::RiskGame.ViewModels.MainViewModel;
using MainWindow = Client::RiskGame.Views.MainWindow;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class InboxIndicatorBuilder : IDisposable
    {
        public const int NoQueries = 0;
        public const int FirstQuery = 1;
        public const int SecondQuery = 2;
        public const int ThirdQuery = 3;
        public const int RegisteredUserId = 41;
        public const int OtherUserId = 43;
        public const int GuestUserId = -41;
        public const string PendingClass = "InboxPending";
        public const string NormalColor = "#333333";
        public const string PendingColor = "#D4A32D";
        private const int WaitSeconds = 5;
        private const int PumpDelayMilliseconds = 1;
        private const string CultureName = "es-MX";
        private const string Username = "inbox.test";
        private const string SessionToken = "inbox-test-token";
        private const string ReplacementToken = "replacement-inbox-token";
        private const string InboxButtonName = "InboxButton";
        private const string RefreshingFieldName = "isRefreshingInbox";
        private const string ConnectionFieldName = "accountConnection";
        private const string NotifyMethodName = "NotifyInboxChanged";
        private readonly SessionUser? previousSession = SessionService.CurrentUser;
        private readonly CultureInfo? previousCulture = Strings.Culture;
        private int inboxEventCount;
        private MainWindow? window;

        public InboxIndicatorBuilder()
        {
            Strings.Culture = new CultureInfo(CultureName);
            Session = new SessionUser(RegisteredUserId, Username, Username) { SessionToken = SessionToken };
            SessionService.Login(Session);
            SessionService.InboxChanged += InboxChanged;
            Client = new RecordingInboxClient();
            ViewModel = new MainViewModel(Client);
        }

        public SessionUser Session { get; }

        public RecordingInboxClient Client { get; }

        public MainViewModel ViewModel { get; }

        public int InboxEventCount => Volatile.Read(ref inboxEventCount);

        public Button InboxButton
        {
            get
            {
                if (window == null)
                {
                    throw new InvalidOperationException("Create the main window before accessing its inbox button.");
                }
                return window.FindControl<Button>(InboxButtonName)!;
            }
        }

        public void RefreshAndWait(int expectedCalls)
        {
            ViewModel.RefreshInboxNotifications();
            WaitForIdle(expectedCalls);
        }

        public void WaitForIdle(int expectedCalls)
        {
            PumpUntil(() => Client.CompletedCalls >= expectedCalls && !IsRefreshing());
        }

        public static void PumpUntil(Func<bool> completed)
        {
            Stopwatch timeout = Stopwatch.StartNew();
            TimeSpan maximumWait = TimeSpan.FromSeconds(WaitSeconds);
            while (!completed() && timeout.Elapsed < maximumWait)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(PumpDelayMilliseconds);
            }
            Dispatcher.UIThread.RunJobs();
            if (!completed())
            {
                throw new TimeoutException("The inbox indicator did not finish its test operation.");
            }
        }

        private bool IsRefreshing()
        {
            FieldInfo refreshingField = typeof(MainViewModel).GetField(RefreshingFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool refreshing = false;
            if (refreshingField.GetValue(ViewModel) is bool currentValue)
            {
                refreshing = currentValue;
            }
            return refreshing;
        }

        public void CreateWindow()
        {
            window = new MainWindow { DataContext = ViewModel };
            window.Show();
            WaitForIdle(FirstQuery);
        }

        public Color ReadButtonColor()
        {
            Color color;
            if (InboxButton.Background is ISolidColorBrush background)
            {
                color = background.Color;
            }
            else
            {
                throw new InvalidOperationException("The inbox indicator must use a solid background brush.");
            }
            return color;
        }

        public void UseGuest()
        {
            SessionService.Login(new SessionUser(GuestUserId, Username, Username));
        }

        public void ClearSession()
        {
            UseGuest();
            SessionService.Logout();
        }

        public static void ReplaceSession()
        {
            SessionService.Login(new SessionUser(OtherUserId, Username, Username)
            {
                SessionToken = ReplacementToken
            });
        }

        public AccountSessionConnection AttachLocalConnection()
        {
            AccountSessionConnection connection = new AccountSessionConnection(Session, UnexpectedExpiry);
            FieldInfo connectionField = typeof(SessionService).GetField(ConnectionFieldName,
                BindingFlags.Static | BindingFlags.NonPublic)!;
            connectionField.SetValue(null, connection);
            return connection;
        }

        public static void NotifyConnection(AccountSessionConnection connection)
        {
            MethodInfo notifyMethod = typeof(SessionService).GetMethod(NotifyMethodName,
                BindingFlags.Static | BindingFlags.NonPublic)!;
            notifyMethod.Invoke(null, new object[] { connection });
        }

        private static void UnexpectedExpiry(AccountSessionConnection connection)
        {
            throw new InvalidOperationException("The indicator test must not start a network session monitor.");
        }

        private void InboxChanged(object? sender, EventArgs eventArgs)
        {
            Interlocked.Increment(ref inboxEventCount);
        }

        public void Dispose()
        {
            try
            {
                ViewModel.StopInboxNotifications();
                Client.ReleaseBlockedQuery();
                PumpUntil(() => Client.CompletedCalls == Client.Calls);
                if (window != null)
                {
                    window.Close();
                }
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                SessionService.InboxChanged -= InboxChanged;
                ClearSession();
                Strings.Culture = previousCulture!;
                if (previousSession != null)
                {
                    SessionService.Login(previousSession);
                }
                Client.Dispose();
            }
        }

        public sealed class RecordingInboxClient : InboxNotificationClient, IDisposable
        {
            private const int QueryWaitSeconds = 5;
            private const int CancellationHandleIndex = 1;
            private readonly object responseLock = new object();
            private readonly Queue<QueryResponse> responses = new Queue<QueryResponse>();
            private readonly ManualResetEventSlim entered = new ManualResetEventSlim();
            private readonly ManualResetEventSlim release = new ManualResetEventSlim();
            private int calls;
            private int completedCalls;
            private int cancellationObserved;

            public int Calls => Volatile.Read(ref calls);

            public int CompletedCalls => Volatile.Read(ref completedCalls);

            public bool CancellationObserved => Volatile.Read(ref cancellationObserved) > NoQueries;

            public bool DefaultPending { get; set; }

            public void Enqueue(bool pending)
            {
                AddResponse(new QueryResponse { Pending = pending });
            }

            public void EnqueueBlocked(bool pending)
            {
                entered.Reset();
                release.Reset();
                AddResponse(new QueryResponse { Pending = pending, WaitForRelease = true });
            }

            public void EnqueueFailure()
            {
                AddResponse(new QueryResponse { Fail = true });
            }

            private void AddResponse(QueryResponse response)
            {
                lock (responseLock)
                {
                    responses.Enqueue(response);
                }
            }

            private QueryResponse ReadResponse()
            {
                QueryResponse response = new QueryResponse { Pending = DefaultPending };
                lock (responseLock)
                {
                    if (responses.Count > NoQueries)
                    {
                        response = responses.Dequeue();
                    }
                }
                return response;
            }

            public override bool HasPending(SessionUser session, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref calls);
                QueryResponse response = ReadResponse();
                try
                {
                    if (response.WaitForRelease)
                    {
                        WaitForRelease(cancellationToken);
                    }
                    if (response.Fail)
                    {
                        throw new RpcException(new Status(StatusCode.Unavailable,
                            "The simulated inbox connection is unavailable."));
                    }
                    return response.Pending;
                }
                finally
                {
                    Interlocked.Increment(ref completedCalls);
                }
            }

            private void WaitForRelease(CancellationToken cancellationToken)
            {
                WaitHandle[] handles = { release.WaitHandle, cancellationToken.WaitHandle };
                entered.Set();
                int selectedHandle = WaitHandle.WaitAny(handles, TimeSpan.FromSeconds(QueryWaitSeconds));
                if (selectedHandle == CancellationHandleIndex)
                {
                    Interlocked.Exchange(ref cancellationObserved, FirstQuery);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (selectedHandle == WaitHandle.WaitTimeout)
                {
                    throw new TimeoutException("The blocked inbox test query was not released.");
                }
            }

            public void WaitUntilBlocked()
            {
                if (!entered.Wait(TimeSpan.FromSeconds(QueryWaitSeconds)))
                {
                    throw new TimeoutException("The inbox query did not enter the expected blocked state.");
                }
            }

            public void ReleaseBlockedQuery()
            {
                release.Set();
            }

            public void Dispose()
            {
                entered.Dispose();
                release.Dispose();
            }

            private sealed class QueryResponse
            {
                public bool Pending { get; init; }

                public bool Fail { get; init; }

                public bool WaitForRelease { get; init; }
            }
        }
    }
}
