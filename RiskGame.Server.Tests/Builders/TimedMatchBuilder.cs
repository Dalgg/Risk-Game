using System;
using System.Collections.Concurrent;
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
    public sealed class TimedMatchBuilder : IDisposable
    {
        public const int FirstUserId = 41;
        public const int SecondUserId = 43;
        public const int DefaultSeconds = 30;
        public const int FirstPairIndex = 1;
        public const int TemporaryColumn = 6;
        public const int SavedSteps = 2;
        private const int MatchId = 601;
        private const int ShortColumn = 2;
        private const int FirstClaim = 3;
        private const int SecondClaim = 12;
        private static readonly int[] ProgressDice = { 3, 3, 4, 4 };
        private static readonly int[] SummitDice = { 1, 1, 1, 1 };
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Task subscription = Task.CompletedTask;
        private readonly string token;

        public TimedMatchBuilder(int turnSeconds = DefaultSeconds)
        {
            Time = new ManualTimeProvider();
            ProfileSessionRegistry sessions = new ProfileSessionRegistry();
            token = sessions.Register(FirstUserId);
            Manager = new MatchSessionManager(sessions, new RecordingMatchRepository(), Time);
            LobbyDetails lobby = new LobbyDetails
            {
                Rules = new LobbyRules { TurnDurationSeconds = turnSeconds }
            };
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = FirstUserId } });
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = SecondUserId } });
            Match = Manager.CreateMatch(MatchId, lobby);
            KeepPlayersConnected();
        }

        private void KeepPlayersConnected()
        {
            if (!Match.IsFinished)
            {
                foreach (MatchParticipant participant in Match.Participants)
                {
                    Match.UpdateConnection(participant.UserId, true);
                }
            }
        }

        public ManualTimeProvider Time { get; }

        public MatchSessionManager Manager { get; }

        public ActiveMatch Match { get; }

        public SnapshotStream Stream { get; } = new SnapshotStream();

        public StopTurnRequest StopRequest()
        {
            return new StopTurnRequest
            {
                MatchId = MatchId, UserId = FirstUserId, SessionToken = token, TurnId = Match.TurnId
            };
        }

        public RollDiceRequest RollRequest()
        {
            return new RollDiceRequest
            {
                MatchId = MatchId, UserId = FirstUserId, SessionToken = token,
                ExpectedRollId = Match.RollId, TurnId = Match.TurnId
            };
        }

        public PairChoice ChoiceRequest()
        {
            return new PairChoice
            {
                MatchId = MatchId, UserId = FirstUserId, SessionToken = token,
                RollId = Match.RollId, TurnId = Match.TurnId, PairIndex = FirstPairIndex
            };
        }

        public void PrepareTemporaryProgress()
        {
            GetColumn(TemporaryColumn).SaveProgress(FirstUserId, SavedSteps);
            Match.ApplyRoll(ProgressDice, Match.RollId);
            Match.ChoosePair(Match.RollId, FirstPairIndex);
        }

        public void PrepareSummit()
        {
            GetColumn(ShortColumn).SaveProgress(FirstUserId, SavedSteps);
            Match.ApplyRoll(SummitDice, Match.RollId);
            Match.ChoosePair(Match.RollId, FirstPairIndex);
        }

        public void FinishMatch()
        {
            MatchColumn first = GetColumn(FirstClaim);
            MatchColumn second = GetColumn(SecondClaim);
            first.SaveProgress(FirstUserId, first.MaximumSteps);
            second.SaveProgress(FirstUserId, second.MaximumSteps);
            PrepareSummit();
            Manager.StopTurn(StopRequest());
        }

        public void BustTurn()
        {
            MatchColumn column = GetColumn(ShortColumn);
            column.SaveProgress(FirstUserId, column.MaximumSteps);
            Match.ApplyRoll(SummitDice, Match.RollId);
        }

        public MatchColumn GetColumn(int columnNumber)
        {
            return Match.Columns.Single(column => column.ColumnNumber == columnNumber);
        }

        public BoardState Subscribe()
        {
            MatchSubscription request = new MatchSubscription
            {
                MatchId = MatchId, UserId = FirstUserId, SessionToken = token
            };
            subscription = Task.Run(() => Manager.SubscribeMatchEvents(request, Stream, cancellation.Token));
            return Stream.Read();
        }

        public MatchTurnTimerService StartTimerService()
        {
            MatchTurnTimerService service = new MatchTurnTimerService(Manager, TimeProvider.System);
            service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            return service;
        }

        public void RaceExpirationAndStop(StopTurnRequest request)
        {
            Task.WaitAll(Task.Run(Manager.ExpireTurns),
                Task.Run(() => Xunit.Record.Exception(() => Manager.StopTurn(request))));
        }

        public void Dispose()
        {
            cancellation.Cancel();
            subscription.GetAwaiter().GetResult();
            cancellation.Dispose();
        }

        public sealed class SnapshotStream : IServerStreamWriter<BoardState>
        {
            private const int WaitSeconds = 5;
            private readonly BlockingCollection<BoardState> snapshots = new BlockingCollection<BoardState>();

            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(BoardState message)
            {
                snapshots.Add(message);
                return Task.CompletedTask;
            }

            public BoardState Read()
            {
                using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                return snapshots.Take(timeout.Token);
            }
        }
    }
}
