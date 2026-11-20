using System.Linq;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchResultBuilderTests
    {
        private const int WinnerId = 41;
        private const int FirstPosition = 1;
        private const int WinningColumns = 3;
        private const int RegisteredPlayers = 2;
        private readonly MatchResultBuilder builder = new MatchResultBuilder();

        [Fact]
        public void Build_RegisteredPlayers_IncludesBothResults()
        {
            ActiveMatch match = new CompletedMatchBuilder().Build();

            int count = builder.Build(match).Count;

            Assert.Equal(RegisteredPlayers, count);
        }

        [Fact]
        public void Build_RegisteredWinner_StoresWinningPosition()
        {
            ActiveMatch match = new CompletedMatchBuilder().Build();

            int? position = builder.Build(match).Single(result => result.IdUser == WinnerId).FinalPosition;

            Assert.Equal(FirstPosition, position);
        }

        [Fact]
        public void Build_RegisteredWinner_StoresClaimedColumnsAsScore()
        {
            ActiveMatch match = new CompletedMatchBuilder().Build();

            int? score = builder.Build(match).Single(result => result.IdUser == WinnerId).FinalScore;

            Assert.Equal(WinningColumns, score);
        }

        [Fact]
        public void Build_GuestOpponent_ExcludesGuestResult()
        {
            ActiveMatch match = new CompletedMatchBuilder().WithGuestOpponent().Build();

            System.Collections.Generic.IReadOnlyList<RiskGame.Models.MatchResult> results = builder.Build(match);

            Assert.Single(results);
        }

        [Fact]
        public void Build_GuestWinner_DoesNotAwardVictoryToRegisteredOpponent()
        {
            ActiveMatch match = new CompletedMatchBuilder().AsGuestWinner().Build();

            bool hasRegisteredWinner = builder.Build(match).Any(result => result.FinalPosition == FirstPosition);

            Assert.False(hasRegisteredWinner);
        }

        [Fact]
        public void Build_OnlyGuests_ReturnsEmptyResults()
        {
            ActiveMatch match = new CompletedMatchBuilder().AsGuestWinner().WithGuestOpponent().Build();

            System.Collections.Generic.IReadOnlyList<RiskGame.Models.MatchResult> results = builder.Build(match);

            Assert.Empty(results);
        }

        [Fact]
        public void Build_UnfinishedMatch_RejectsResults()
        {
            ActiveMatch match = new CompletedMatchBuilder().CreateReadyMatch();

            System.Exception? rejectedException = Record.Exception(() => builder.Build(match));

            Assert.IsType<MatchActionException>(rejectedException);
        }
    }
}
