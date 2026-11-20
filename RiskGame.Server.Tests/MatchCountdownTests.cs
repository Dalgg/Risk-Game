extern alias Client;

using System;
using RiskGame.Server.Tests.Builders;
using Xunit;
using BoardState = Client::RiskGame.Contracts.Match.BoardState;
using MatchCountdownViewModel = Client::RiskGame.ViewModels.MatchCountdownViewModel;

namespace RiskGame.Server.Tests
{
    public sealed class MatchCountdownTests
    {
        private const int MatchId = 601;
        private const long FirstTurn = 1;
        private const long NextTurn = 2;
        private const long FullDurationMilliseconds = 30000;
        private const long FractionalDurationMilliseconds = 29999;
        private const int FullDurationSeconds = 30;
        private const int ElapsedSeconds = 10;
        private const int RemainingSeconds = 20;
        private const int AfterDeadlineSeconds = 40;
        private const int NoSeconds = 0;
        private readonly ManualTimeProvider time = new ManualTimeProvider();
        private readonly MatchCountdownViewModel countdown;

        public MatchCountdownTests()
        {
            countdown = new MatchCountdownViewModel(time);
            countdown.ApplySnapshot(CreateSnapshot(FirstTurn));
        }

        [Fact]
        public void ApplySnapshot_NewTurn_ShowsServerTime()
        {
            Assert.Equal(FullDurationSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void Refresh_ElapsedTime_DecreasesCounter()
        {
            time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            countdown.Refresh();

            Assert.Equal(RemainingSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void Refresh_PastDeadline_StaysAtZero()
        {
            time.Advance(TimeSpan.FromSeconds(AfterDeadlineSeconds));

            countdown.Refresh();

            Assert.Equal(NoSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void Refresh_AtDeadline_DisablesActions()
        {
            time.Advance(TimeSpan.FromSeconds(FullDurationSeconds));

            countdown.Refresh();

            Assert.False(countdown.HasTimeRemaining);
        }

        [Fact]
        public void ApplySnapshot_SameTurn_DoesNotResetCounter()
        {
            time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            countdown.ApplySnapshot(CreateSnapshot(FirstTurn));

            Assert.Equal(RemainingSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void ApplySnapshot_NextTurn_ResetsCounter()
        {
            time.Advance(TimeSpan.FromSeconds(FullDurationSeconds));
            countdown.Refresh();

            countdown.ApplySnapshot(CreateSnapshot(NextTurn));

            Assert.Equal(FullDurationSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void ApplySnapshot_FinishedMatch_HidesCounter()
        {
            BoardState snapshot = CreateSnapshot(NextTurn);
            snapshot.IsFinished = true;

            countdown.ApplySnapshot(snapshot);

            Assert.False(countdown.IsVisible);
        }

        [Fact]
        public void ApplySnapshot_FinishedMatch_StopsCounter()
        {
            BoardState snapshot = CreateSnapshot(NextTurn);
            snapshot.IsFinished = true;

            countdown.ApplySnapshot(snapshot);

            Assert.Equal(NoSeconds, countdown.RemainingSeconds);
        }

        [Fact]
        public void ApplySnapshot_FractionalSeconds_RoundsUp()
        {
            BoardState snapshot = CreateSnapshot(NextTurn);
            snapshot.TurnRemainingMilliseconds = FractionalDurationMilliseconds;

            countdown.ApplySnapshot(snapshot);

            Assert.Equal(FullDurationSeconds, countdown.RemainingSeconds);
        }

        private static BoardState CreateSnapshot(long turnId)
        {
            return new BoardState
            {
                MatchId = MatchId, TurnId = turnId, TurnRemainingMilliseconds = FullDurationMilliseconds
            };
        }
    }
}
