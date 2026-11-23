using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class DisconnectedMatchBuilder
    {
        public const int FirstUserId = 41;
        public const int SecondUserId = 43;
        public const int ThirdUserId = 47;
        public const int MatchId = 901;
        private const int TurnSeconds = 90;
        private const int FirstPair = 1;
        private static readonly int[] ProgressDice = { 3, 3, 4, 4 };
        private readonly Dictionary<int, string> tokens = new Dictionary<int, string>();

        public DisconnectedMatchBuilder()
        {
            Time = new ManualTimeProvider();
            ProfileSessionRegistry registry = new ProfileSessionRegistry();
            Repository = new RecordingMatchRepository();
            Manager = new MatchSessionManager(registry, Repository, Time);
            LobbyDetails lobby = new LobbyDetails { Rules = new LobbyRules { TurnDurationSeconds = TurnSeconds } };
            foreach (int userId in new[] { FirstUserId, SecondUserId, ThirdUserId })
            {
                tokens.Add(userId, registry.Register(userId));
                lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = userId } });
            }
            Match = Manager.CreateMatch(MatchId, lobby);
            KeepConnected(FirstUserId);
            KeepConnected(SecondUserId);
            KeepConnected(ThirdUserId);
        }

        public ManualTimeProvider Time { get; }

        public ActiveMatch Match { get; }

        public RecordingMatchRepository Repository { get; }

        public MatchSessionManager Manager { get; }

        public void KeepConnected(int userId)
        {
            Match.UpdateConnection(userId, true);
        }

        public void PrepareProgress()
        {
            Match.ApplyRoll(ProgressDice, Match.RollId);
            Match.ChoosePair(Match.RollId, FirstPair);
        }

        public MatchSubscription Request(int userId)
        {
            return new MatchSubscription { MatchId = MatchId, UserId = userId, SessionToken = tokens[userId] };
        }

        public void ExpireFirstPlayer()
        {
            Match.UpdateConnection(FirstUserId, false);
            Time.Advance(TimeSpan.FromSeconds(MatchParticipant.ReconnectionGraceSeconds));
            Manager.ExpireTurns();
        }

        public void BroadcastRoll()
        {
            MatchSubscription identity = Request(FirstUserId);
            Manager.RollDice(new RollDiceRequest
            {
                MatchId = MatchId, UserId = FirstUserId, SessionToken = identity.SessionToken,
                ExpectedRollId = Match.RollId, TurnId = Match.TurnId
            });
        }

        public void AttemptSubscribe(int userId)
        {
            using CancellationTokenSource cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Manager.SubscribeMatchEvents(Request(userId), new SnapshotStream(), cancelled.Token)
                .GetAwaiter().GetResult();
        }

        public SubscriptionProbe Subscribe(int userId)
        {
            return new SubscriptionProbe(Manager, Request(userId));
        }

        public sealed class SubscriptionProbe : IDisposable
        {
            private const int CompletionWaitSeconds = 5;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly SnapshotStream stream = new SnapshotStream();
            private readonly Task subscription;

            public SubscriptionProbe(MatchSessionManager manager, MatchSubscription request)
            {
                subscription = Task.Run(() => manager.SubscribeMatchEvents(request, stream, cancellation.Token));
                InitialSnapshot = stream.Read();
            }

            public BoardState InitialSnapshot { get; }

            public int MessageCount => stream.MessageCount;

            public BoardState Read()
            {
                return stream.Read();
            }

            public void FailNextWrite()
            {
                stream.FailNextWrite();
            }

            public BoardPlayer ReadDisconnectedPlayer(int userId)
            {
                return stream.ReadDisconnectedPlayer(userId);
            }

            public bool WaitForCompletion()
            {
                subscription.WaitAsync(TimeSpan.FromSeconds(CompletionWaitSeconds)).GetAwaiter().GetResult();
                return subscription.IsCompletedSuccessfully;
            }

            public void Dispose()
            {
                cancellation.Cancel();
                subscription.GetAwaiter().GetResult();
                cancellation.Dispose();
            }
        }

        private sealed class SnapshotStream : IServerStreamWriter<BoardState>
        {
            private const int WaitSeconds = 5;
            private const int NoFailure = 0;
            private const int FailWrite = 1;
            private readonly BlockingCollection<BoardState> snapshots = new BlockingCollection<BoardState>();
            private int messageCount;
            private int failNextWrite;

            public WriteOptions? WriteOptions { get; set; }

            public int MessageCount => Volatile.Read(ref messageCount);

            public Task WriteAsync(BoardState message)
            {
                if (Interlocked.Exchange(ref failNextWrite, NoFailure) == FailWrite)
                {
                    throw new IOException("Simulated disconnected match stream.");
                }
                Interlocked.Increment(ref messageCount);
                snapshots.Add(message);
                return Task.CompletedTask;
            }

            public void FailNextWrite()
            {
                Interlocked.Exchange(ref failNextWrite, FailWrite);
            }

            public BoardPlayer ReadDisconnectedPlayer(int userId)
            {
                using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                BoardPlayer player;
                do
                {
                    BoardState snapshot = snapshots.Take(timeout.Token);
                    player = snapshot.Players.Single(candidate => candidate.UserId == userId);
                }
                while (player.IsConnected);
                return player;
            }

            public BoardState Read()
            {
                using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                return snapshots.Take(timeout.Token);
            }
        }
    }
}
