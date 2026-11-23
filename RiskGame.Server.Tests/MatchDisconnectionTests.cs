using System;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchDisconnectionTests
    {
        private const int FirstUser = DisconnectedMatchBuilder.FirstUserId;
        private const int SecondUser = DisconnectedMatchBuilder.SecondUserId;
        private const int ThirdUser = DisconnectedMatchBuilder.ThirdUserId;
        private const int BeforeGraceSeconds = 29;
        private const int IdleBeyondTurnSeconds = 120;
        private const int GraceSeconds = 30;
        private const int NoWinner = 0;
        private const int SingleMessage = 1;
        private const int CompletionRetrySeconds = 2;

        [Fact]
        public void Disconnect_WithinGrace_KeepsParticipant()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));

            setup.Match.ProcessConnectionTimeouts();

            Assert.False(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void Reconnect_WithinGrace_KeepsTemporaryProgress()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.PrepareProgress();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));

            setup.AttemptSubscribe(FirstUser);

            Assert.NotEmpty(setup.Match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void Reconnect_AfterGrace_RejectsParticipant()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.ExpireFirstPlayer();

            Assert.Throws<MatchActionException>(() => setup.AttemptSubscribe(FirstUser));
        }

        [Fact]
        public void GraceExpired_CurrentPlayer_PassesTurn()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();

            setup.ExpireFirstPlayer();

            Assert.Equal(SecondUser, setup.Match.CurrentTurnUserId);
        }

        [Fact]
        public void GraceExpired_CurrentPlayer_DiscardsTemporaryProgress()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.PrepareProgress();

            setup.ExpireFirstPlayer();

            Assert.Empty(setup.Match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void Abandon_NonCurrentPlayer_PreservesCurrentProgress()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.PrepareProgress();

            setup.Match.AbandonParticipant(SecondUser);

            Assert.NotEmpty(setup.Match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void AdvanceTurn_AbandonedNextPlayer_SkipsParticipant()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.PrepareProgress();
            setup.Match.AbandonParticipant(SecondUser);

            setup.Match.StopTurn(setup.Match.TurnId);

            Assert.Equal(ThirdUser, setup.Match.CurrentTurnUserId);
        }

        [Fact]
        public void Abandon_LastOpponent_DeclaresConnectedWinner()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.AbandonParticipant(SecondUser);

            setup.Match.AbandonParticipant(ThirdUser);

            Assert.Equal(FirstUser, setup.Match.WinnerUserId);
        }

        [Fact]
        public void Abandon_LastOpponent_RecordsAbandonmentReason()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.AbandonParticipant(SecondUser);

            setup.Match.AbandonParticipant(ThirdUser);

            Assert.Equal(MatchEndReason.Abandonment, setup.Match.EndReason);
        }

        [Fact]
        public void Disconnect_AllParticipants_ExpiresTogetherWithoutWinner()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Match.UpdateConnection(SecondUser, false);
            setup.Match.UpdateConnection(ThirdUser, false);
            setup.Time.Advance(TimeSpan.FromSeconds(GraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(NoWinner, setup.Match.WinnerUserId);
        }

        [Fact]
        public void Disconnect_AllParticipants_RecordsCancellationReason()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Match.UpdateConnection(SecondUser, false);
            setup.Match.UpdateConnection(ThirdUser, false);
            setup.Time.Advance(TimeSpan.FromSeconds(GraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(MatchEndReason.Cancelled, setup.Match.EndReason);
        }

        [Fact]
        public void Subscribe_IdleBeyondTurnDeadline_RemainsConnected()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);
            setup.Time.Advance(TimeSpan.FromSeconds(IdleBeyondTurnSeconds));

            setup.Manager.ExpireTurns();

            Assert.True(setup.Match.GetParticipant(FirstUser).IsConnected);
        }

        [Fact]
        public void Subscribe_IdleBeyondTurnDeadline_DoesNotAbandonParticipant()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);
            setup.Time.Advance(TimeSpan.FromSeconds(IdleBeyondTurnSeconds));

            setup.Manager.ExpireTurns();

            Assert.False(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void Subscribe_IdleBeforeTurnExpiry_DoesNotSendSnapshot()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);
            setup.Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.Equal(SingleMessage, stream.MessageCount);
        }

        [Fact]
        public void Subscribe_Cancelled_MarksDisconnected()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);

            stream.Dispose();

            Assert.False(setup.Match.GetParticipant(FirstUser).IsConnected);
        }

        [Fact]
        public void Subscribe_CancelledAfterIdle_StartsFreshGrace()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);
            setup.Time.Advance(TimeSpan.FromSeconds(IdleBeyondTurnSeconds));
            stream.Dispose();
            setup.Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.False(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void Subscribe_Cancelled_AbandonsAfterGrace()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);
            stream.Dispose();
            setup.Time.Advance(TimeSpan.FromSeconds(GraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.True(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void Subscribe_WriteFails_MarksDisconnected()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe failing = setup.Subscribe(FirstUser);
            using DisconnectedMatchBuilder.SubscriptionProbe observer = setup.Subscribe(SecondUser);
            failing.Read();
            failing.FailNextWrite();

            setup.BroadcastRoll();
            BoardPlayer disconnected = observer.ReadDisconnectedPlayer(FirstUser);

            Assert.False(disconnected.IsConnected);
        }

        [Fact]
        public void Subscribe_WriteFails_KeepsGraceBeforeAbandonment()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe failing = setup.Subscribe(FirstUser);
            using DisconnectedMatchBuilder.SubscriptionProbe observer = setup.Subscribe(SecondUser);
            failing.Read();
            failing.FailNextWrite();
            setup.BroadcastRoll();
            observer.ReadDisconnectedPlayer(FirstUser);
            setup.Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));

            setup.Manager.ExpireTurns();

            Assert.False(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void Subscribe_ReplaceOldStream_OldCleanupDoesNotDisconnectNewStream()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            DisconnectedMatchBuilder.SubscriptionProbe original = setup.Subscribe(FirstUser);
            using DisconnectedMatchBuilder.SubscriptionProbe replacement = setup.Subscribe(FirstUser);

            original.Dispose();

            Assert.True(setup.Match.GetParticipant(FirstUser).IsConnected);
        }

        [Fact]
        public void LeaveMatch_ValidSession_AbandonsImmediately()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();

            setup.Manager.LeaveMatch(setup.Request(FirstUser));

            Assert.True(setup.Match.GetParticipant(FirstUser).HasAbandoned);
        }

        [Fact]
        public void LeaveMatch_TokenForAnotherPlayer_Rejects()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            MatchSubscription request = setup.Request(FirstUser);
            request.UserId = SecondUser;

            Assert.Throws<InvalidSessionException>(() => setup.Manager.LeaveMatch(request));
        }

        [Fact]
        public void TakeAbandonedParticipants_RepeatedExpiration_QueuesOnce()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.ExpireFirstPlayer();

            setup.Manager.ExpireTurns();

            Assert.Single(setup.Manager.TakeAbandonedParticipants());
        }

        [Fact]
        public void AutomaticCompletion_DatabaseFailure_RetriesAfterDelay()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.AbandonParticipant(SecondUser);
            setup.Match.AbandonParticipant(ThirdUser);
            setup.Repository.FailNextSave();
            setup.Manager.ExpireTurns();

            setup.Time.Advance(TimeSpan.FromSeconds(CompletionRetrySeconds));
            setup.Manager.ExpireTurns();

            Assert.Equal(SingleMessage, setup.Repository.SavedMatches);
        }

        [Fact]
        public void Abandon_StreamStillOpen_ReleasesServerSubscription()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe stream = setup.Subscribe(FirstUser);

            setup.Manager.LeaveMatch(setup.Request(FirstUser));

            Assert.True(stream.WaitForCompletion());
        }

        [Fact]
        public void Reconnect_AbandonedParticipantInFinishedMatch_Rejects()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.AbandonParticipant(SecondUser);
            setup.Match.AbandonParticipant(ThirdUser);

            Assert.Throws<MatchActionException>(() => setup.AttemptSubscribe(SecondUser));
        }

        [Fact]
        public void Abandon_LastParticipantDisconnected_WaitsForReconnect()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Match.AbandonParticipant(SecondUser);

            setup.Match.AbandonParticipant(ThirdUser);

            Assert.False(setup.Match.IsFinished);
        }

        [Fact]
        public void Reconnect_OnlyRemainingParticipant_DeclaresWinner()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();
            setup.Match.UpdateConnection(FirstUser, false);
            setup.Match.AbandonParticipant(SecondUser);
            setup.Match.AbandonParticipant(ThirdUser);

            setup.AttemptSubscribe(FirstUser);

            Assert.Equal(FirstUser, setup.Match.WinnerUserId);
        }
    }
}
