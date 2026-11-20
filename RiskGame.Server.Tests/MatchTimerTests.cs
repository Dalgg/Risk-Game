using System;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchTimerTests
    {
        private const int ShortSeconds = 30;
        private const int DefaultSeconds = 60;
        private const int LongSeconds = 90;
        private const int OneSecond = 1;
        private const int RemainingSeconds = 20;
        private const int ElapsedSeconds = 10;
        private const int ShortColumn = 2;
        private const long NextTurnId = 2;
        private const long FullRotationTurnId = 3;
        private static readonly int[] PendingDice = { 3, 3, 4, 4 };

        [Theory]
        [InlineData(ShortSeconds)]
        [InlineData(DefaultSeconds)]
        [InlineData(LongSeconds)]
        public void RemainingTime_NewMatch_UsesSelectedDuration(int seconds)
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder(seconds);

            Assert.Equal(TimeSpan.FromSeconds(seconds), setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void RemainingTime_Elapsed_Decreases()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            Assert.Equal(TimeSpan.FromSeconds(RemainingSeconds), setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void ExpireTurns_BeforeDeadline_KeepsPlayer()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds - OneSecond));

            setup.Manager.ExpireTurns();

            Assert.Equal(TimedMatchBuilder.FirstUserId, setup.Match.CurrentTurnUserId);
        }

        [Theory]
        [InlineData(ShortSeconds)]
        [InlineData(DefaultSeconds)]
        [InlineData(LongSeconds)]
        public void ExpireTurns_AtDeadline_PassesTurn(int seconds)
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder(seconds);
            setup.Time.Advance(TimeSpan.FromSeconds(seconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(TimedMatchBuilder.SecondUserId, setup.Match.CurrentTurnUserId);
        }

        [Fact]
        public void ExpireTurns_Expired_DiscardsTemporaryProgress()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.Empty(setup.Match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void ExpireTurns_Expired_KeepsSavedProgress()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(TimedMatchBuilder.SavedSteps,
                setup.GetColumn(TimedMatchBuilder.TemporaryColumn).StepsByUserId[TimedMatchBuilder.FirstUserId]);
        }

        [Fact]
        public void ExpireTurns_AtSummit_DoesNotClaimColumn()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareSummit();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.False(setup.GetColumn(ShortColumn).IsClaimed);
        }

        [Fact]
        public void ExpireTurns_PendingPair_ClearsDice()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Match.ApplyRoll(PendingDice, setup.Match.RollId);
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.Empty(setup.Match.Dice);
        }

        [Fact]
        public void ExpireTurns_Expired_RecordsTimeout()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.True(setup.Match.LastTurnTimedOut);
        }

        [Fact]
        public void ExpireTurns_RepeatedCheck_AdvancesOnce()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();
            setup.Manager.ExpireTurns();

            Assert.Equal(NextTurnId, setup.Match.TurnId);
        }

        [Fact]
        public void ApplyRoll_Bust_RestartsClock()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            setup.BustTurn();

            Assert.Equal(TimeSpan.FromSeconds(ShortSeconds), setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void ApplyRoll_Bust_DoesNotReportTimeout()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();

            setup.BustTurn();

            Assert.False(setup.Match.LastTurnTimedOut);
        }

        [Fact]
        public void ExpireTurns_NextTurn_StartsWithFullDuration()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(TimeSpan.FromSeconds(ShortSeconds), setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void StopTurn_AtDeadlineBeforeTimerTick_RejectsAction()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            StopTurnRequest request = setup.StopRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            Assert.Throws<MatchActionException>(() => setup.Manager.StopTurn(request));
        }

        [Fact]
        public void StopTurn_AtDeadlineBeforeTimerTick_ExpiresTurn()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            StopTurnRequest request = setup.StopRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            Record.Exception(() => setup.Manager.StopTurn(request));

            Assert.Equal(TimedMatchBuilder.SecondUserId, setup.Match.CurrentTurnUserId);
        }

        [Fact]
        public void StopTurn_BeforeDeadline_RestartsClock()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            setup.Time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            setup.Manager.StopTurn(setup.StopRequest());

            Assert.Equal(TimeSpan.FromSeconds(ShortSeconds), setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void StopTurn_BeforeDeadline_OldDeadlineDoesNotExpireNextTurn()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            setup.Time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));
            setup.Manager.StopTurn(setup.StopRequest());
            setup.Time.Advance(TimeSpan.FromSeconds(RemainingSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(NextTurnId, setup.Match.TurnId);
        }

        [Fact]
        public void RollDice_AtDeadline_RejectsLateRequest()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            RollDiceRequest request = setup.RollRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            Assert.Throws<MatchActionException>(() => setup.Manager.RollDice(request));
        }

        [Fact]
        public void ChoosePair_AtDeadline_RejectsLateRequest()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Match.ApplyRoll(PendingDice, setup.Match.RollId);
            PairChoice request = setup.ChoiceRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            Assert.Throws<MatchActionException>(() => setup.Manager.ChooseDicePair(request));
        }

        [Fact]
        public void RollDice_AfterFullRotation_RejectsRequestFromOldTurn()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            RollDiceRequest request = setup.RollRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));
            setup.Manager.ExpireTurns();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));
            setup.Manager.ExpireTurns();

            Assert.Throws<MatchActionException>(() => setup.Manager.RollDice(request));
        }

        [Fact]
        public void ExpireTurns_FullRotation_ReturnsToFirstPlayer()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));
            setup.Manager.ExpireTurns();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(FullRotationTurnId, setup.Match.TurnId);
        }

        [Fact]
        public void ExpireTurns_FinishedMatch_DoesNotAdvance()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.FinishMatch();
            long finishedTurnId = setup.Match.TurnId;
            setup.Time.Advance(TimeSpan.FromSeconds(LongSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(finishedTurnId, setup.Match.TurnId);
        }

        [Fact]
        public void RemainingTime_FinishedMatch_IsZero()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();

            setup.FinishMatch();

            Assert.Equal(TimeSpan.Zero, setup.Match.GetRemainingTurnTime());
        }

        [Fact]
        public void Subscribe_ExistingTurn_DoesNotResetClock()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Time.Advance(TimeSpan.FromSeconds(ElapsedSeconds));

            BoardState snapshot = setup.Subscribe();

            Assert.Equal((long)TimeSpan.FromSeconds(RemainingSeconds).TotalMilliseconds,
                snapshot.TurnRemainingMilliseconds);
        }

        [Fact]
        public void ExpireTurns_Expired_NotifiesSubscribers()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Subscribe();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.Manager.ExpireTurns();

            Assert.True(setup.Stream.Read().LastTurnTimedOut);
        }

        [Fact]
        public void TimerService_ExpiredTurn_AutomaticallyNotifiesSubscribers()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.Subscribe();
            using MatchTurnTimerService service = setup.StartTimerService();

            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            Assert.Equal(TimedMatchBuilder.SecondUserId, setup.Stream.Read().CurrentTurnUserId);
        }

        [Fact]
        public void ExpireTurns_ConcurrentLateStop_AdvancesOnlyOnce()
        {
            using TimedMatchBuilder setup = new TimedMatchBuilder();
            setup.PrepareTemporaryProgress();
            StopTurnRequest request = setup.StopRequest();
            setup.Time.Advance(TimeSpan.FromSeconds(ShortSeconds));

            setup.RaceExpirationAndStop(request);

            Assert.Equal(NextTurnId, setup.Match.TurnId);
        }
    }
}
