extern alias Client;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Grpc.Core;
using BoardState = Client::RiskGame.Contracts.Match.BoardState;
using BoardPlayer = Client::RiskGame.Contracts.Match.BoardPlayer;
using DataOperationException = Client::RiskGame.Exceptions.DataOperationException;
using IMatchCallback = Client::RiskGame.Callbacks.IMatchCallback;
using MatchSubscription = Client::RiskGame.Contracts.Match.MatchSubscription;
using MatchViewModel = Client::RiskGame.ViewModels.MatchViewModel;
using MatchWindow = Client::RiskGame.Views.MatchWindow;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class MatchReconnectBuilder : IDisposable
    {
        public const int SingleAttempt = 1;
        public const int TwoAttempts = 2;
        public const int InitialRevision = 1;
        public const int UpdatedRevision = 2;
        private const int MatchId = 901;
        private const int UserId = 41;
        private const int GuestId = -41;
        private const int RepeatedClicks = 25;
        private const int RemainingMilliseconds = 90000;
        private const int MaximumMarkers = 3;
        private const int WaitSeconds = 5;
        private const int PollMilliseconds = 10;
        private const int FirstIndex = 0;
        private const string Username = "reconnect.user";
        private const string Nickname = "Reconnect";
        private const string ButtonName = "ReconnectButton";
        private const string StatusTextBlockName = "MatchStatusTextBlock";
        private const string SimulatedActionFailure = "Simulated match action failure.";
        private readonly object streamsLock = new object();
        private readonly List<FakeMatchStream> streams = new List<FakeMatchStream>();
        private readonly SessionUser? previousSession = SessionService.CurrentUser;

        public MatchReconnectBuilder() : this(TimeSpan.FromSeconds(WaitSeconds))
        {
        }

        public MatchReconnectBuilder(TimeSpan connectionTimeout)
        {
            SessionService.Login(new SessionUser(UserId, Username, Nickname));
            ViewModel = new MatchViewModel(OpenStream, connectionTimeout);
            Window = new MatchWindow(MatchId, ViewModel);
        }

        public MatchViewModel ViewModel { get; }

        public MatchWindow Window { get; }

        public RecordingCallback Callback { get; } = new RecordingCallback();

        public Button ReconnectButton => Window.FindControl<Button>(ButtonName)!;

        public int AttemptCount
        {
            get
            {
                lock (streamsLock)
                {
                    return streams.Count;
                }
            }
        }

        public FakeMatchStream LatestStream
        {
            get
            {
                lock (streamsLock)
                {
                    return streams.Last();
                }
            }
        }

        public bool Subscribe()
        {
            return ViewModel.Subscribe(MatchId, Callback);
        }

        public void ShowWindow()
        {
            Window.Show();
            WaitForAttempts(SingleAttempt);
        }

        public void RepeatReconnectClicks()
        {
            for (int clickIndex = FirstIndex; clickIndex < RepeatedClicks; clickIndex++)
            {
                ClickReconnect();
            }
        }

        public void ClickReconnect()
        {
            ReconnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        public void WaitForAttempts(int expectedCount)
        {
            WaitUntil(() => AttemptCount == expectedCount);
        }

        public void WaitForButtonEnabled()
        {
            WaitUntil(() => ReconnectButton.IsEnabled);
        }

        public void WaitForSynchronized()
        {
            WaitUntil(() => Window.FindControl<TextBlock>(StatusTextBlockName)!.Text == Strings.MatchSynchronized);
        }

        public DataOperationException CreateActionError(StatusCode statusCode)
        {
            RpcException rpcException = new RpcException(new Status(statusCode, SimulatedActionFailure));
            return ViewModel.CreateActionException(rpcException);
        }

        public void FailAction(StatusCode statusCode)
        {
            DataOperationException exception = CreateActionError(statusCode);
            Window.ShowActionError(exception);
            Dispatcher.UIThread.RunJobs();
        }

        public static BoardState CreateSnapshot(int revision)
        {
            BoardState snapshot = new BoardState
            {
                MatchId = MatchId, Revision = revision, CurrentTurnUserId = UserId,
                TurnId = InitialRevision, TurnRemainingMilliseconds = RemainingMilliseconds,
                MaximumTemporaryMarkers = MaximumMarkers
            };
            snapshot.Players.Add(new BoardPlayer { UserId = UserId, Nickname = Nickname, IsConnected = true });
            return snapshot;
        }

        public static void WaitUntil(Func<bool> condition)
        {
            Stopwatch waiting = Stopwatch.StartNew();
            Dispatcher.UIThread.RunJobs();
            while (!condition() && waiting.Elapsed < TimeSpan.FromSeconds(WaitSeconds))
            {
                Thread.Sleep(PollMilliseconds);
                Dispatcher.UIThread.RunJobs();
            }
            if (!condition())
            {
                throw new TimeoutException("The reconnect test did not reach the expected state.");
            }
        }

        private AsyncServerStreamingCall<BoardState> OpenStream(MatchSubscription subscription,
            CancellationToken cancellationToken)
        {
            FakeMatchStream stream = new FakeMatchStream(cancellationToken);
            lock (streamsLock)
            {
                streams.Add(stream);
            }
            return new AsyncServerStreamingCall<BoardState>(stream, Task.FromResult(Metadata.Empty),
                GetStatus, GetTrailers, stream.Dispose);
        }

        private static Status GetStatus()
        {
            return Status.DefaultSuccess;
        }

        private static Metadata GetTrailers()
        {
            return Metadata.Empty;
        }

        public void Dispose()
        {
            Window.MarkLeftForShutdown();
            Window.Close();
            ViewModel.StopSubscription();
            WaitUntil(AllStreamsDisposed);
            if (previousSession != null)
            {
                SessionService.Login(previousSession);
            }
            else
            {
                SessionService.Login(new SessionUser(GuestId, string.Empty, string.Empty));
                SessionService.Logout();
            }
        }

        private bool AllStreamsDisposed()
        {
            lock (streamsLock)
            {
                return streams.All(stream => stream.IsDisposed);
            }
        }

        public sealed class FakeMatchStream : IAsyncStreamReader<BoardState>, IDisposable
        {
            private const int NotDisposed = 0;
            private const int Disposed = 1;
            private readonly CancellationToken connectionCancellation;
            private readonly BlockingCollection<Task<BoardState>> snapshots =
                new BlockingCollection<Task<BoardState>>();
            private int disposed = NotDisposed;

            public FakeMatchStream(CancellationToken connectionCancellation)
            {
                this.connectionCancellation = connectionCancellation;
            }

            public BoardState Current { get; private set; } = new BoardState();

            public bool IsDisposed => Volatile.Read(ref disposed) == Disposed;

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return Task.Run(() => ReadSnapshot(cancellationToken));
            }

            public void SendSnapshot(int revision)
            {
                snapshots.Add(Task.FromResult(CreateSnapshot(revision)));
            }

            public void Fail()
            {
                snapshots.Add(Task.FromException<BoardState>(new RpcException(
                    new Status(StatusCode.Unavailable, "Simulated disconnected stream."))));
            }

            private bool ReadSnapshot(CancellationToken cancellationToken)
            {
                using CancellationTokenSource reading = CancellationTokenSource.CreateLinkedTokenSource(
                    connectionCancellation, cancellationToken);
                Current = snapshots.Take(reading.Token).GetAwaiter().GetResult();
                return true;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref disposed, Disposed);
            }
        }

        public sealed class RecordingCallback : IMatchCallback
        {
            private long latestRevision;

            public long LatestRevision => Interlocked.Read(ref latestRevision);

            public void MatchUpdated(BoardState boardState)
            {
                Interlocked.Exchange(ref latestRevision, boardState.Revision);
            }

            public void MatchStreamStopped(string statusMessage)
            {
                LastError = statusMessage;
            }

            public string LastError { get; private set; } = string.Empty;
        }
    }
}
