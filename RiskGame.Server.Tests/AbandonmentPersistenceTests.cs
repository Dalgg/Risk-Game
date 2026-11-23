using System.Linq;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class AbandonmentPersistenceTests
    {
        private const int FirstPlace = 1;
        private const int SecondPlace = 2;
        private const int RegisteredPlayers = 2;
        private const int OneRegisteredPlayer = 1;
        private const int NoStates = 0;

        [Fact]
        public void SaveCompletedMatch_Abandonment_StoresReason()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment());

            Assert.Equal("Abandonment", setup.ReadMatch().EndReason);
        }

        [Fact]
        public void SaveCompletedMatch_Abandonment_ClosesMatch()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment());

            Assert.True(setup.ReadMatch().EndDate.HasValue);
        }

        [Fact]
        public void SaveCompletedMatch_Abandonment_AssignsWinner()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment());

            Assert.Equal(PersistedAbandonmentBuilder.WinnerId,
                setup.ReadResults().Single(result => result.FinalPosition == FirstPlace).IdUser);
        }

        [Fact]
        public void SaveCompletedMatch_Abandonment_AssignsLoser()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment());

            Assert.Equal(PersistedAbandonmentBuilder.OpponentId,
                setup.ReadResults().Single(result => result.FinalPosition == SecondPlace).IdUser);
        }

        [Fact]
        public void SaveCompletedMatch_Abandonment_RemovesState()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment());

            Assert.Equal(NoStates, setup.CountStates());
        }

        [Fact]
        public void SaveCompletedMatch_Abandonment_RetryDoesNotDuplicateResults()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();
            ActiveMatch match = setup.CreateAbandonment();
            setup.Repository.SaveCompletedMatch(match);

            setup.Repository.SaveCompletedMatch(match);

            Assert.Equal(RegisteredPlayers, setup.ReadResults().Count);
        }

        [Fact]
        public void SaveCompletedMatch_GuestWinner_DoesNotInsertGuest()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment(true));

            Assert.Single(setup.ReadResults());
        }

        [Fact]
        public void SaveCompletedMatch_GuestWinner_DoesNotAwardRegisteredOpponent()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateAbandonment(true));

            Assert.Equal(SecondPlace, setup.ReadResults().Single().FinalPosition);
        }

        [Fact]
        public void SaveCompletedMatch_Cancelled_StoresReason()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateCancellation());

            Assert.Equal("Cancelled", setup.ReadMatch().EndReason);
        }

        [Fact]
        public void SaveCompletedMatch_Cancelled_ClosesMatch()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateCancellation());

            Assert.True(setup.ReadMatch().EndDate.HasValue);
        }

        [Fact]
        public void SaveCompletedMatch_Cancelled_DoesNotInsertResults()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateCancellation());

            Assert.Empty(setup.ReadResults());
        }

        [Fact]
        public void SaveCompletedMatch_Cancelled_RemovesState()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateCancellation());

            Assert.Equal(NoStates, setup.CountStates());
        }

        [Fact]
        public void SaveCompletedMatch_Cancelled_RetryDoesNotInsertResults()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();
            ActiveMatch match = setup.CreateCancellation();
            setup.Repository.SaveCompletedMatch(match);

            setup.Repository.SaveCompletedMatch(match);

            Assert.Empty(setup.ReadResults());
        }

        [Fact]
        public void SaveCompletedMatch_Victory_PreservesReason()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();

            setup.Repository.SaveCompletedMatch(setup.CreateVictory());

            Assert.Equal("Victory", setup.ReadMatch().EndReason);
        }

        [Fact]
        public void SaveCompletedMatch_Unfinished_RejectsPersistence()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();
            ActiveMatch match = setup.CreateActiveMatch();

            Assert.Throws<MatchActionException>(() => setup.Repository.SaveCompletedMatch(match));
        }

        [Fact]
        public void BuildResults_Cancelled_ReturnsEmptyCollection()
        {
            using PersistedAbandonmentBuilder setup = new PersistedAbandonmentBuilder();
            ActiveMatch match = setup.CreateCancellation();

            Assert.Empty(new MatchResultBuilder().Build(match));
        }
    }
}
