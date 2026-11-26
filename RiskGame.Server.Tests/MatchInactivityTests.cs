using System.Linq;
using RiskGame.Contracts.Match;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchInactivityTests
    {
        [Fact]
        public void Timeout_FirstOwnTurn_DoesNotAbandonParticipant()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();

            setup.ExpireCurrentTurn();

            Assert.False(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_AnotherPlayersTurn_DoesNotCountAsSecondOwnAbsence()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.ExpireCurrentTurn();

            setup.ExpireCurrentTurn();

            Assert.False(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_SecondConsecutiveOwnAbsence_AbandonsParticipant()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();

            setup.ExpireCurrentTurn();

            Assert.True(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_ThreePlayers_AdvancesToNextRemainingParticipant()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder(InactivityMatchBuilder.ThreePlayers);
            setup.MissFirstTurnAndReturn();

            setup.ExpireCurrentTurn();

            Assert.Equal(InactivityMatchBuilder.SecondUserId, setup.Match.CurrentTurnUserId);
        }

        [Fact]
        public void ValidRoll_AfterAbsence_ResetsConsecutiveAbsences()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();
            setup.CompleteCurrentTurnWithRoll();
            setup.ReturnToFirstPlayer();

            setup.ExpireCurrentTurn();

            Assert.False(setup.FirstParticipant.HasAbandoned);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidRoll_AfterAbsence_DoesNotResetCounter(bool outdatedRequest)
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();
            setup.AttemptInvalidRoll(outdatedRequest);

            setup.ExpireCurrentTurn();

            Assert.True(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Reconnection_AfterAbsence_DoesNotResetCounter()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();
            setup.ReconnectFirstPlayer();

            setup.ExpireCurrentTurn();

            Assert.True(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_AlreadyDisconnected_PreservesRemainingReconnectionGrace()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();

            setup.DisconnectFirstBeforeTimeout();

            Assert.False(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_AfterRollingWithoutChoosingPair_DoesNotCountAbsence()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.RollCurrentTurn();
            setup.ExpireCurrentTurn();
            setup.ReturnToFirstPlayer();

            setup.ExpireCurrentTurn();

            Assert.False(setup.FirstParticipant.HasAbandoned);
        }

        [Fact]
        public void Timeout_LastOpponentAbandons_DeclaresRemainingPlayerWinner()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            setup.MissFirstTurnAndReturn();

            setup.ExpireCurrentTurn();

            Assert.Equal(InactivityMatchBuilder.SecondUserId, setup.Match.WinnerUserId);
        }

        [Fact]
        public void Timeout_AbandonsParticipant_NotifiesObserver()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder(InactivityMatchBuilder.ThreePlayers);
            using DisconnectedMatchBuilder.SubscriptionProbe observer = setup.SubscribeObserver();
            setup.MissFirstTurnAndReturn();

            setup.ExpireCurrentTurn();
            BoardState snapshot = InactivityMatchBuilder.ReadAfterAbandonment(observer);

            Assert.True(snapshot.Players.Single(player => player.UserId == InactivityMatchBuilder.FirstUserId)
                .HasAbandoned);
        }

        [Fact]
        public void Timeout_ResultSaveFails_StillNotifiesWinner()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder();
            using DisconnectedMatchBuilder.SubscriptionProbe observer = setup.SubscribeObserver();
            setup.MissFirstTurnAndReturn();
            setup.Repository.FailNextSave();

            setup.ExpireCurrentTurn();
            BoardState snapshot = InactivityMatchBuilder.ReadAfterAbandonment(observer);

            Assert.Equal(InactivityMatchBuilder.SecondUserId, snapshot.WinnerUserId);
        }

        [Fact]
        public void Timeout_RepeatedTimerChecks_QueuesAbandonmentOnce()
        {
            InactivityMatchBuilder setup = new InactivityMatchBuilder(InactivityMatchBuilder.ThreePlayers);
            setup.MissFirstTurnAndReturn();
            setup.ExpireCurrentTurn();

            setup.Manager.ExpireTurns();
            setup.Manager.ExpireTurns();

            Assert.Single(setup.Manager.TakeAbandonedParticipants());
        }
    }
}
